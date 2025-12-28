#pragma once

#include "utils/esp_utility.hpp"

template<typename TBacklighController>
class backlight_timer_t
{
    static constexpr char TAG[] = "BL";

public:
    backlight_timer_t(TBacklighController& backlight_controller, uint64_t timeout_ms)
        : _backlight_controller(backlight_controller)
        , _timeout_ms(timeout_ms)
        , _timer(create_timer())
    {
    }

    void init()
    {
        ESP_ERROR_CHECK(esp_timer_start_once(*_timer, _timeout_ms * 1000));
    }

    void kick()
    {
        _backlight_controller.backlight(true);
        restart_timer();
    }

    void set_timeout(uint64_t timeout_ms)
    {
        if (timeout_ms == _timeout_ms)
            return;
        
        _timeout_ms = timeout_ms;
        restart_timer();
    }

private:
    void restart_timer()
    {
        auto err = esp_timer_stop(*_timer);
        if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
            ESP_LOGW(TAG, "esp_timer_stop failed: %s", esp_err_to_name(err));
        }

        err = esp_timer_start_once(*_timer, _timeout_ms * 1000);
        if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
            ESP_LOGW(TAG, "esp_timer_start_once failed: %s", esp_err_to_name(err));
        }
    }

    esp_timer_ptr create_timer()
    {
        return make_esp_timer({
            .callback = THIS_CALLBACK(this, backlight_off),
            .arg = this,
            .name = "bl_off",
        });
    }

    void backlight_off()
    {
        ESP_LOGI(TAG, "OFF");
        _backlight_controller.backlight(false);
    }

private:
    TBacklighController& _backlight_controller;
    uint64_t _timeout_ms;
    esp_timer_ptr _timer;
};