#pragma once

#include "esp_timer.h"

#include "esp_utility.hpp"

template<typename TBacklighController>
class backlight_timer_t
{
public:
    backlight_timer_t(TBacklighController& backlight_controller, uint64_t timeout_ms)
        : _backlight_controller(backlight_controller)
        , _timeout_ms(timeout_ms)
    {
        const esp_timer_create_args_t args = {
            .callback = THIS_CALLBACK(this, backlight_off),
            .arg = this,
            .name = "bl_off",
        };
        ESP_ERROR_CHECK(esp_timer_create(&args, &_timer));
    }

    void init()
    {
        ESP_ERROR_CHECK(esp_timer_start_once(_timer, _timeout_ms * 1000));
    }

    void kick()
    {
        _backlight_controller.backlight(true);

        auto err = esp_timer_stop(_timer);
        if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
            ESP_LOGW("BL", "esp_timer_stop failed: %s", esp_err_to_name(err));
        }

        err = esp_timer_start_once(_timer, 30 * 1000 * 1000);
        if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
            ESP_LOGW("BL", "esp_timer_start_once failed: %s", esp_err_to_name(err));
        }
    }

private:
    void backlight_off()
    {
        _backlight_controller.backlight(false);
    }

private:
    TBacklighController& _backlight_controller;
    uint64_t _timeout_ms;
    esp_timer_handle_t _timer;
};