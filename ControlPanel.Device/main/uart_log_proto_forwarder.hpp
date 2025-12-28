#pragma once

#include <cstdarg>
#include <cstdio>
#include <cstring>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "esp_log.h"
#include "esp_rom_sys.h"

#include "esp_utility.hpp"
#include "protocol/frame_host_connection.hpp"
#include "protocol.hpp"

class uart_log_proto_forwarder
{
    static constexpr size_t LOG_LINE_MAX = 256;
    static constexpr size_t LOG_QUEUE_LEN = 32;

    struct log_line_t
    {
        uint16_t len;
        char buf[LOG_LINE_MAX];
    };

public:
    static void init(auto& fp)
    {
        _send = [&](auto d) { fp.send(d, 100, 1); };
        _queue = xQueueCreateStatic(LOG_QUEUE_LEN, sizeof(log_line_t), _storage, &_static_queue);

        xTaskCreate(log_forward_task, "log_fwd", 4096, nullptr, tskIDLE_PRIORITY + 1, &_log_task);

        esp_log_set_vprintf(&log_vprintf_hook);
    }
private:
    static int log_vprintf_hook(const char* fmt, va_list ap)
    {
        if (__atomic_exchange_n(&_in_hook, 1, __ATOMIC_ACQ_REL) != 0) {
            return 0;
        }

        log_line_t line{};

        va_list ap_sz;
        va_copy(ap_sz, ap);
        auto sz = vsnprintf(nullptr, 0, fmt, ap_sz);
        va_end(ap_sz);

        char buf[sz + 1];

        va_list ap_str;
        va_copy(ap_str, ap);
        vsnprintf(buf, sz + 1, fmt, ap_str);
        va_end(ap_str);

        for (auto i = 0; i < sz / (LOG_LINE_MAX - 1); i++)
        {
            line.len = LOG_LINE_MAX - 1;
            std::memcpy(line.buf, buf + i * (LOG_LINE_MAX - 1), LOG_LINE_MAX - 1);
            line.buf[LOG_LINE_MAX - 1] = 0;

            enqueue_line(line);
        }

        auto remain = sz % (LOG_LINE_MAX - 1);
        if (remain > 0)
        {
            line.len = remain;
            std::memcpy(line.buf, buf + (sz - remain), remain);
            line.buf[remain] = 0;

            enqueue_line(line);
        }
        
        __atomic_store_n(&_in_hook, 0, __ATOMIC_RELEASE);

        return sz;
    }

    static void enqueue_line(log_line_t& line)
    {
        if (xPortInIsrContext())
        {
            BaseType_t task_woken = pdFALSE;
            xQueueSendFromISR(_queue, &line, &task_woken);
            if (task_woken) portYIELD_FROM_ISR();
        } else
        {
            xQueueSend(_queue, &line, 0);
        }
    }

    static auto scoped_log_disable(const char* tag)
    {
        auto ll = esp_log_level_get(tag);
        esp_log_level_set(tag, esp_log_level_t::ESP_LOG_NONE);
        return scoped_fn([=](){ esp_log_level_set(tag, ll); });
    }

    static void log_forward_task(void*)
    {
        log_line_t line{};

        while (true)
        {
            if (!xQueueReceive(_queue, &line, portMAX_DELAY))
                continue;

            log_message_t msg{};
            msg.line = std::string(line.buf, line.len);

            {
                //auto send_ll = scoped_log_disable(uart_t::SEND_TAG);
                //auto sz_ll = scoped_log_disable(uart_t::SEND_TAG);

                auto bytes = serialize_bridge_message(msg);
                _send(bytes);
            }
        }
    }

private:
    inline static StaticQueue_t _static_queue;
    inline static uint8_t _storage[LOG_QUEUE_LEN * sizeof(log_line_t)];
    inline static QueueHandle_t _queue = nullptr;
    inline static volatile bool _in_hook = 0;
    inline static TaskHandle_t _log_task = nullptr;
    inline static std::function<void(std::span<uint8_t>)> _send;
};