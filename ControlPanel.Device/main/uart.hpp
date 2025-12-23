#pragma once

#include <byteswap.h>

#include <type_traits>
#include <functional>
#include <atomic>

#include "driver/uart.h"

#include "framer.hpp"
#include "buffer_queue.hpp"
#include "esp_utility.hpp"

class uart_t
{
    static constexpr uint8_t MAGIC[] = {0x19, 0x16};
    static constexpr size_t MAX_FRAME = 32 * 1024;
    static constexpr size_t MAX_TX_FRAME = 256;

    static constexpr uint32_t SEND_QUEUE_SIZE = 8;

    struct frame_data_t
    {
        uint16_t seq;
        uint32_t retry_interval;
        uint32_t retry_count;
        frame_type_t type;
        std::size_t data_size;
        std::span<uint8_t> block;
    };

public:
    static constexpr char TAG[] = "UART";
    static constexpr char SEND_TAG[] = "UART >>";

    uart_t(uart_port_t port, gpio_num_t tx, gpio_num_t rx, int buffer_size, int baud_rate)
        : _port(port), _buffer_queue(MAX_TX_FRAME, SEND_QUEUE_SIZE), _framer{MAGIC, MAX_FRAME}
    {
        const uart_config_t cfg = {
            .baud_rate  = baud_rate,
            .data_bits  = UART_DATA_8_BITS,
            .parity     = UART_PARITY_DISABLE,
            .stop_bits  = UART_STOP_BITS_1,
            .flow_ctrl  = UART_HW_FLOWCTRL_DISABLE,
            .source_clk = UART_SCLK_APB,
        };

        configASSERT(_send_queue = xQueueCreate(SEND_QUEUE_SIZE, sizeof(frame_data_t)));

        uart_param_config(_port, &cfg);
        uart_set_pin(_port, tx, rx, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE);
        uart_driver_install(_port, buffer_size, 0, 20, &_uart_rx_queue, ESP_INTR_FLAG_IRAM);
    }

    template<typename F>
    void register_data_handler(F&& cb)
    {
        _data_handler = std::move(cb);
    }

    void init(void)
    {
        xTaskCreate(THIS_CALLBACK(this, uart_send_task), "uart_send_task", 4096, this, 10, &_send_task);
        xTaskCreate(THIS_CALLBACK(this, uart_event_task), "uart_event_task", 4096, this, 10, NULL);
    }

    inline void send_data(std::span<uint8_t> data, uint32_t retry_interval_ms = 1000, uint32_t retry_count = 3)
    {
        auto frame_size = _framer.calc_frame_size(data.size());

        if (frame_size > _buffer_queue.block_size())
        {
            ESP_LOGE(SEND_TAG, "data too large sz=%d frame_sz=%d block_sz=%d", data.size(), frame_size, _buffer_queue.block_size());
            return;
        }
        
        auto block = _buffer_queue.take();
        std::copy_n(data.data(), data.size(), block.data());

        {
            std::scoped_lock lock{_send_sync};

            frame_data_t frame_data{_seq_cnt++, retry_interval_ms, retry_count, frame_type_t::data, data.size(), block};
            if (!xQueueSend(_send_queue, &frame_data, portMAX_DELAY))
                ESP_LOGE(SEND_TAG, "%s", "Unable to enqueue frame");
        }
    }

private:
    void write_uart(std::span<uint8_t> bytes)
    {
        uart_write_bytes(_port, bytes.data(), bytes.size());
    }

    void uart_send_task()
    {
        const std::size_t max_pending_acks = 64;

        std::vector<uint8_t> bytes(MAX_TX_FRAME);
        frame_data_t frame_data;
        std::set<uint8_t> pending_acks;

        while (true)
        {
            if (!xQueueReceive(_send_queue, &frame_data, portMAX_DELAY))
                continue;

            frame_t frame{frame_data.seq, frame_data.type, frame_data.block.subspan(0, frame_data.data_size)};
            scoped_fn give_data([&](){ _buffer_queue.give(frame_data.block); });

            auto need = _framer.calc_frame_size(frame.data.size());
            if (bytes.capacity() < need)
            {
                ESP_LOGE(TAG, "send loop: data too large sz=%d frame_sz=%d buffer_sz=%d", frame.data.size(), need, bytes.capacity());
                continue;
            }

            bytes.clear();
            _framer.to_bytes(bytes, frame);

            uint16_t i;
            for (i = 0; i < frame_data.retry_count; i++)
            {
                write_uart(bytes);
                
                uint32_t ack_seq;
                if (xTaskNotifyWait(0, 0xFFFFFFFF, &ack_seq, pdMS_TO_TICKS(frame_data.retry_interval)))
                {
                    if (ack_seq == frame_data.seq)
                    {
                        ESP_LOGD(TAG, "frame seq=%d ACKed", frame_data.seq);
                        break;
                    }

                    if (pending_acks.erase(ack_seq))
                    {
                        ESP_LOGD(TAG, "frame seq=%d ACKed", frame_data.seq);
                        break;
                    }

                    if (ack_seq < frame_data.seq)
                    {
                        if (pending_acks.size() > max_pending_acks)
                            pending_acks.clear();
                        
                        pending_acks.emplace(ack_seq);
                        continue;
                    }

                    ESP_LOGW(TAG, "ack on different message seq=%d ack=%d", frame_data.seq, (uint16_t)ack_seq);
                }
            }

            if (i == frame_data.retry_count)
            {
                ESP_LOGE(TAG, "%s", "Unable to send frame, no reposne");
            }
        }
    }

    void uart_event_task()
    {
        uart_event_t event;
        std::array<uint8_t, 1024> tmp{};

        while(true)
        {
            if (xQueueReceive(_uart_rx_queue, &event, portMAX_DELAY) != pdTRUE)
                continue;

            switch (event.type)
            {
            case UART_DATA:
            {
                auto to_read = event.size;
                while (to_read > 0) {
                    auto chunk = std::min(to_read, tmp.size());
                    auto got = uart_read_bytes(_port, tmp.data(), chunk, 0);
                    if (got <= 0) break;
                    to_read -= got;

                    _framer.feed(tmp.data(), (size_t)got, [&](const frame_t& frame) {
                        switch (frame.type)
                        {
                            case frame_type_t::ack:
                                ESP_LOGD(TAG, "new frame ack seq=%d len=%d", frame.seq, frame.data.size());
                                xTaskNotify(_send_task, (uint32_t)frame.seq, eSetValueWithOverwrite);

                                break;
                            case frame_type_t::data:
                                ESP_LOGD(TAG, "new frame data seq=%d len=%d", frame.seq, frame.data.size());

                                std::vector<uint8_t> ack(_framer.calc_frame_size(0));
                                _framer.to_bytes(ack, frame_t{frame.seq, frame_type_t::ack, {}});
                                write_uart(ack);
                                
                                if (_data_handler) _data_handler(frame.data);
                                
                                break;
                        }
                    });
                }
                break;
            }

            case UART_FIFO_OVF:
            case UART_BUFFER_FULL:
                ESP_LOGW(TAG, "%s", "overflow");
                uart_flush_input(_port);
                xQueueReset(_uart_rx_queue);
                _framer.reset();
                break;

            case UART_PARITY_ERR:
            case UART_FRAME_ERR:
                ESP_LOGW(TAG, "%s", "parity/frame error");
                _framer.reset();
                break;

            default:
                break;
            }
        }
    }

private:
    uart_port_t _port;
    QueueHandle_t _uart_rx_queue;
    QueueHandle_t _send_queue;
    buffer_queue_t _buffer_queue;
    TaskHandle_t _send_task;
    uart_framer_t<sizeof(MAGIC)> _framer;
    std::function<void(std::span<const uint8_t>)> _data_handler;

    std::recursive_mutex _send_sync;
    std::atomic<uint16_t> _seq_cnt = 0;
};