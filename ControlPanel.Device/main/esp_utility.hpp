#pragma once

#include "esp_log.h"
#include "esp_err.h"

#define ESP_ASSERT_CHECK(x, tag, msg) ({\
        if (unlikely(!x)) {\
            ESP_LOGE(tag, "%s", msg);\
            configASSERT(x);\
        }\
    })
#define ESP_LOGI_FLUSH(...) do {ESP_LOGI(__VA_ARGS__); fflush(stdout); uart_wait_tx_done(UART_NUM_0, pdMS_TO_TICKS(100));} while(0)
#define THIS_CALLBACK(that, fn) +[](void* a) {\
    ESP_ASSERT_CHECK(a, "CALLBACK", "'this' is null");\
    ((decltype(that))a)->fn();\
}
