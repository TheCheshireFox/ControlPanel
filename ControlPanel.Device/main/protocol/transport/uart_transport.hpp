#pragma once

#include "driver/uart.h"

#include "utils/esp_utility.hpp"

namespace transport
{
    struct uart_transport_t
    {
        static constexpr char TAG[] = "UART";

        uart_transport_t(uart_port_t port, gpio_num_t tx, gpio_num_t rx, int buffer_size, int baud_rate)
            : _port(port)
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
            uart_set_pin(_port, tx, rx, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE);
            uart_driver_install(_port, buffer_size / 2, buffer_size / 2, 20, &_uart_rx_queue, ESP_INTR_FLAG_IRAM);
        }

        void init()
        {
            xTaskCreate(THIS_CALLBACK(this, uart_event_task), "uart_event_task", 4096, this, 10, NULL);
        }

        void write(std::span<uint8_t> data)
        {
            uart_write_bytes(_port, data.data(), data.size());
        }

        template<typename F>
        void on_receive(F&& f)
        {
            _on_recieve = std::move(f);
        }

    private:
        void uart_event_task()
        {
            uart_event_t event;
            std::array<uint8_t, 1024> buffer{};

            while(true)
            {
                if (xQueueReceive(_uart_rx_queue, &event, portMAX_DELAY) != pdTRUE)
                    continue;

                switch (event.type)
                {
                case UART_DATA:
                {
                    while (true) {
                        auto read = uart_read_bytes(_port, buffer.data(), buffer.size(), 0);
                        if (read <= 0)
                            break;

                        if (_on_recieve) _on_recieve(std::span<uint8_t>(buffer.data(), read));
                    }
                    break;
                }

                case UART_FIFO_OVF:
                case UART_BUFFER_FULL:
                    ESP_LOGW(TAG, "%s", "overflow");
                    uart_flush_input(_port);
                    xQueueReset(_uart_rx_queue);
                    break;

                case UART_PARITY_ERR:
                case UART_FRAME_ERR:
                    ESP_LOGW(TAG, "%s", "parity/frame error");
                    break;

                default:
                    break;
                }
            }
        }

    private:
        uart_port_t _port;
        QueueHandle_t _uart_rx_queue;
        std::function<void(std::span<uint8_t>)> _on_recieve{};
    };
};