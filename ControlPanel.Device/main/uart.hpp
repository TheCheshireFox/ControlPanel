#pragma once

#include <byteswap.h>

#include <type_traits>
#include <functional>
#include <atomic>

#include "driver/uart.h"

#include "framer.hpp"
#include "ack_waiter.hpp"
#include "esp_utility.hpp"

template<bool enabled>
struct uart_lock_t
{
    static void init()
    {
        if constexpr (enabled)
        {
            esp_log_set_vprintf(locked_vprintf);
        }
    }

    inline void lock() { if constexpr (enabled) { _sync.lock(); } }
    inline void unlock() { if constexpr (enabled) { _sync.unlock(); } }

private:
    static int locked_vprintf(const char *fmt, va_list ap)
    {
        if (xPortInIsrContext()) {
            return 0;
        }

        std::unique_lock lock{_sync};
        return vprintf(fmt, ap);
    }

private:
    inline static std::recursive_mutex _sync;
};

uart_lock_t<CONFIG_LOG_MAXIMUM_LEVEL != ESP_LOG_NONE> uart_lock;

class uart_t
{
    static constexpr char TAG[] = "UART";

    static constexpr uint8_t MAGIC[] = {0x19, 0x16};
    static constexpr size_t  MAX_FRAME = 32 * 1024;

public:
    uart_t(uart_port_t port, int buffer_size, int baud_rate)
        : _port(port), _framer{MAGIC, MAX_FRAME}
    {
        const uart_config_t cfg = {
            .baud_rate  = baud_rate,
            .data_bits  = UART_DATA_8_BITS,
            .parity     = UART_PARITY_DISABLE,
            .stop_bits  = UART_STOP_BITS_1,
            .flow_ctrl  = UART_HW_FLOWCTRL_DISABLE,
            .source_clk = UART_SCLK_APB,
        };

        uart_param_config(_port, &cfg);
        uart_set_pin(_port, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE);
        uart_driver_install(_port, buffer_size, 0, 20, &_queue, ESP_INTR_FLAG_IRAM);
    }

    template<typename F>
    void register_data_handler(F&& cb)
    {
        _data_handler = std::move(cb);
    }

    void init(void)
    {
        uart_lock.init();
        xTaskCreate(THIS_CALLBACK(this, uart_event_task), "uart_event_task", 4096, this, 10, NULL);
    }

    inline void send_data(std::span<uint8_t> data)
    {
        frame_t frame{_seq_cnt++, frame_type_t::data, data};

        std::vector<uint8_t> bytes(_framer.calc_frame_size(data.size()));
        _framer.to_bytes(bytes, frame);

        for (int i = 0; i < 3; i++)
        {
            write_uart(bytes);
            if (_ack_waiter.wait(frame.seq, 1000))
            {
                ESP_LOGD(TAG, "frame seq=%d ACKed", frame.seq);
                break;
            }
        }

        ESP_LOGE(TAG, "%s", "Unable to send frame, no reposne");
    }

private:
    void write_uart(std::span<uint8_t> bytes)
    {
        std::unique_lock lock{uart_lock};
        uart_write_bytes(_port, bytes.data(), bytes.size());
        uart_wait_tx_done(_port, pdMS_TO_TICKS(100));
    }

    void uart_event_task()
    {
        uart_event_t event;
        std::array<uint8_t, 1024> tmp{};

        while(true)
        {
            if (xQueueReceive(_queue, &event, portMAX_DELAY) != pdTRUE)
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
                                _ack_waiter.notify(frame.seq);
                                
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
                xQueueReset(_queue);
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
    QueueHandle_t _queue;
    ack_waiter_t _ack_waiter;
    uart_framer_t<sizeof(MAGIC)> _framer;
    std::function<void(std::span<const uint8_t>)> _data_handler;

    std::atomic<uint16_t> _seq_cnt = 0;
};