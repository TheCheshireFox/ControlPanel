#pragma once

#include <memory>

#include "esp_timer.h"
#include "esp_log.h"
#include "esp_err.h"

#define THIS_CALLBACK(that, fn) +[](void* a) {\
    if (!a) { ESP_LOGE("CALLBACK", "%s", "'this' is null"); configASSERT(a); }\
    ((decltype(that))a)->fn();\
}

using esp_timer_ptr = std::unique_ptr<esp_timer_handle_t, void(*)(esp_timer_handle_t*)>;

esp_timer_ptr make_esp_timer(const esp_timer_create_args_t& args)
{
    static auto deleter = +[](esp_timer_handle_t* timer)
    {
        if (timer == nullptr) return;
        if (*timer != nullptr)
        {
            esp_timer_stop(*timer);
            esp_timer_delete(*timer);
        }
        delete timer;
    };

    auto ptr = esp_timer_ptr(new esp_timer_handle_t(nullptr), deleter);
    ESP_ERROR_CHECK(esp_timer_create(&args, &*ptr));

    return ptr;
}