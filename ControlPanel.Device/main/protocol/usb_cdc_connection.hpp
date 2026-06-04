#pragma once

#include <stdint.h>
#include <functional>
#include <condition_variable>
#include "esp_log.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "tinyusb.h"
#include "tinyusb_default_config.h"
#include "tinyusb_cdc_acm.h"
#include "sdkconfig.h"
#include "frame_buffer.hpp"
#include "framer.hpp"

namespace transport
{
    template<tinyusb_cdcacm_itf_t Port>
    class usb_cdc_connection_t
    {
    public:
        static constexpr char TAG[] = "USB CDC";

    public:
        esp_err_t init(uint16_t rx_queue_size = 16)
        {
            ESP_RETURN_ON_FALSE(_instance == nullptr, ESP_FAIL, TAG, "Connection already created on port %d", static_cast<int>(Port));

            _queue = xQueueCreate(rx_queue_size, sizeof(rx_message_t));
            ESP_RETURN_ON_FALSE(_queue, ESP_FAIL, TAG, "Unable to allocate rx queue");

            auto ret = xTaskCreate(THIS_CALLBACK(this, rx_task), TAG, 4096, this, 0, &_rx_task);
            ESP_RETURN_ON_FALSE(ret == pdTRUE, ESP_FAIL, TAG, "Unable to create rx task");

            constexpr auto tusb_cfg = TINYUSB_DEFAULT_CONFIG();
            ESP_RETURN_ON_ERROR(tinyusb_driver_install(&tusb_cfg), TAG, "Unable to install tinyusb driver");
            tinyusb_config_cdcacm_t acm_cfg = {
                .cdc_port = Port,
                .callback_rx = [](auto itf, auto event) { if (_instance) _instance->rx_cb(itf, event); },
                .callback_rx_wanted_char = nullptr,
                .callback_line_state_changed = [](auto itf, auto event) { if (_instance) _instance->state_changed_cb(itf, event); },
                .callback_line_coding_changed = nullptr
            };
            ESP_RETURN_ON_ERROR(tinyusb_cdcacm_init(&acm_cfg), TAG, "Unable to init cdcacm");

            _instance = this;
            return ESP_OK;
        }

        esp_err_t send(std::span<const uint8_t> data)
        {
            std::unique_lock lock{_state_sync};
            if (!_connected)
                _state_cv.wait(lock, [this] { return _connected; });

            frame_builder_t frame_builder(data);

            std::span<const uint8_t> buffer;
            while (frame_builder.next(buffer))
            {
                while (buffer.size() > 0)
                {
                    auto len = tinyusb_cdcacm_write_queue(Port, buffer.data(), buffer.size());
                    buffer = buffer.subspan(len);
                }
            }

            return tinyusb_cdcacm_write_flush(Port, std::numeric_limits<uint32_t>::max());
        }

        template<typename F>
        void register_data_handler(F&& f)
        {
            _on_data_received = std::forward<F>(f);
        }

        ~usb_cdc_connection_t()
        {
            _instance = nullptr;
            xTaskAbortDelay(_rx_task);
        }

    private:
        void rx_task()
        {
            rx_message_t msg;

            while (true)
            {
                if (!xQueueReceive(_queue, &msg, portMAX_DELAY))
                {
                    vQueueDelete(_queue);
                    vTaskDelete(nullptr);
                    return;
                }

                std::span<const uint8_t> frame_data;
                if (!_framer.try_insert_and_parse({msg.buf, msg.len}, frame_data))
                    continue;

                if (_on_data_received)
                    _on_data_received(frame_data);
            }
        }

        void rx_cb(int itf, cdcacm_event_t*)
        {
            rx_message_t msg;

            auto ret = tinyusb_cdcacm_read(static_cast<tinyusb_cdcacm_itf_t>(itf), msg.buf, CONFIG_TINYUSB_CDC_RX_BUFSIZE, &msg.len);
            ESP_RETURN_VOID_ON_ERROR(ret, TAG, "Unable to read cdc data");

            xQueueSend(_queue, &msg, portMAX_DELAY);
        }

        void state_changed_cb(int, cdcacm_event_t* event)
        {
            if (!event)
                return;

            if (event->type != CDC_EVENT_LINE_STATE_CHANGED)
                return;

            ESP_LOGW(TAG, "Line state changed: %d", event->line_state_changed_data.dtr);
            {
                std::scoped_lock lock{_state_sync};
                _connected = event->line_state_changed_data.dtr;
            }
            _state_cv.notify_all();
        }

    private:
        std::function<void(std::span<const uint8_t>)> _on_data_received;
        framer_t<4096> _framer{};
        QueueHandle_t _queue = nullptr;
        TaskHandle_t _rx_task = nullptr;
        bool _connected = false;
        std::mutex _state_sync{};
        std::condition_variable _state_cv{};

        inline static usb_cdc_connection_t* _instance = nullptr;

        struct rx_message_t
        {
            uint8_t buf[CONFIG_TINYUSB_CDC_RX_BUFSIZE + 1];
            size_t len;
        };
    };
}
