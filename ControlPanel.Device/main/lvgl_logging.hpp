#pragma once 

#include "lvgl.h"
#include "sdkconfig.h"

#ifdef CONFIG_LV_USE_LOG

static void lvgl_init_logging() {
    struct lvgl_log_rec_t{ lv_log_level_t level; char buf[128]; };
    static QueueHandle_t log_queue = xQueueCreate(32, sizeof(lvgl_log_rec_t));

    xTaskCreatePinnedToCore(+[](void*)
    {
        lvgl_log_rec_t rec;
        while(1)
        {
            if (xQueueReceive(log_queue, &rec, portMAX_DELAY))
            {
                switch (rec.level) {
                    case LV_LOG_LEVEL_TRACE: ESP_LOGD("LVGL", "%s", rec.buf); break;
                    case LV_LOG_LEVEL_INFO: ESP_LOGI("LVGL", "%s", rec.buf); break;
                    case LV_LOG_LEVEL_WARN: ESP_LOGW("LVGL", "%s", rec.buf); break;
                    case LV_LOG_LEVEL_ERROR: ESP_LOGW("LVGL", "%s", rec.buf); break;
                    case LV_LOG_LEVEL_USER: ESP_LOGI("LVGL", "%s", rec.buf); break;
                    default: ESP_LOGI("LVGL", "%s", rec.buf); break;
                }
            }
        }
    }, "lvgl_log", 4096, nullptr, 1, nullptr, tskNO_AFFINITY);

    lv_log_register_print_cb(+[](lv_log_level_t level, const char * buf)
    {
        lvgl_log_rec_t rec;
        rec.level = level;
        auto sz = std::min((size_t)(sizeof(rec.buf) - 1), strlen(buf));
        memcpy(rec.buf, buf, sz);
        rec.buf[sz] = 0;
        xQueueGenericSend(log_queue, &rec, pdMS_TO_TICKS(1000), queueSEND_TO_BACK);
    });
}

#else

static void lvgl_init_logging() {}

#endif