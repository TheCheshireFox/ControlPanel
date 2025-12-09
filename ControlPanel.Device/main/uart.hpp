#pragma once

#include <functional>

#include "driver/uart.h"

#include "esp_utility.hpp"

class uart_t
{
public:
    uart_t(uart_port_t port, int buffer_size)
        : _port(port)
    {
        const uart_config_t cfg = {
            .baud_rate  = 115200,
            .data_bits  = UART_DATA_8_BITS,
            .parity     = UART_PARITY_DISABLE,
            .stop_bits  = UART_STOP_BITS_1,
            .flow_ctrl  = UART_HW_FLOWCTRL_DISABLE,
            .source_clk = UART_SCLK_APB,
        };

        // Configure UART0 params (baud etc.)
        uart_param_config(_port, &cfg);

        // Keep the default pins used for boot/logging
        uart_set_pin(_port,
                    UART_PIN_NO_CHANGE,  // TX
                    UART_PIN_NO_CHANGE,  // RX
                    UART_PIN_NO_CHANGE,  // RTS
                    UART_PIN_NO_CHANGE); // CTS

        // Install driver with RX/TX buffers and an event queue
        uart_driver_install(_port,
                            buffer_size,    // rx buffer
                            buffer_size,    // tx buffer
                            20,                   // event queue size
                            &_queue,
                            ESP_INTR_FLAG_IRAM);                   // intr flags
    }

    void register_line_handler(std::function<void(const std::string&)> cb)
    {
        _line_handler = std::move(cb);
    }

    void init(void)
    {
        // Create task that handles RX "interrupts" via events
        xTaskCreate(THIS_CALLBACK(this, uart_event_task), "uart_event_task", 4096, this, 10, NULL);
    }

    inline void send_line(const std::string& s)
    {
        uart_write_bytes(_port, s.c_str(), s.size());
        const char nl = '\n';
        uart_write_bytes(_port, &nl, 1);
    }

private:
    void uart_event_task()
    {
        uart_event_t event;
        static uint8_t rx_buf[128];
        static std::string line_acc;   // or your own C buffer + index

        while (true)
        {
            if (xQueueReceive(_queue, &event, portMAX_DELAY))
            {
                switch (event.type)
                {
                    case UART_DATA: {
                        auto len = uart_read_bytes(_port, rx_buf, std::min(event.size, (size_t)sizeof(rx_buf)), pdMS_TO_TICKS(20));
                        if (len <= 0)
                            break;

                        for (int i = 0; i < len; ++i)
                        {
                            line_acc.push_back(rx_buf[i]);
                            
                            if (rx_buf[i] != '\n')
                                continue;

                            if (_line_handler) _line_handler(line_acc);
                            line_acc.clear();
                        }
                        break;
                }

                default:
                    break;
                }
            }
        }
    }

private:
    uart_port_t _port;
    QueueHandle_t _queue;
    std::function<void(const std::string&)> _line_handler;
};