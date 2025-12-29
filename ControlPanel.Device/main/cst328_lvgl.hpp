#pragma once

#include "lvgl.h"
#include "cst328_driver.hpp"

namespace cts328
{
    namespace details
    {
        struct cts328_lvgl_touch_data_t
        {
            cst328_driver_t* driver;
            bool invert_x;
            bool invert_y;
            bool swap_xy;
            uint32_t touch_timeout;
        };
    }

    static lv_indev_t* lvgl_create_indev(cst328_driver_t* driver, bool invert_x = false, bool invert_y = false, bool swap_xy = false, uint32_t touch_timeout = 40)
    {
        auto touch_data = (details::cts328_lvgl_touch_data_t*)lv_malloc(sizeof(details::cts328_lvgl_touch_data_t));
        touch_data->driver = driver;
        touch_data->invert_x = invert_x;
        touch_data->invert_y = invert_y;
        touch_data->swap_xy = swap_xy;
        touch_data->touch_timeout = touch_timeout;

        auto touch_indev = lv_indev_create();
        lv_indev_set_type(touch_indev, LV_INDEV_TYPE_POINTER);
        lv_indev_set_user_data(touch_indev, touch_data);
        
        lv_indev_set_read_cb(touch_indev, +[](lv_indev_t* indev, lv_indev_data_t *data)
        {
            auto touch_data = (details::cts328_lvgl_touch_data_t*)lv_indev_get_user_data(indev);

            if (!touch_data || !touch_data->driver) {
                data->state = LV_INDEV_STATE_RELEASED;
                return;
            }

            auto pt = touch_data->driver->get_touch();
            auto touched = ((esp_timer_get_time() / 1000) - pt.last_touch_ms) < touch_data->touch_timeout;

            data->state = touched ? LV_INDEV_STATE_PRESSED : LV_INDEV_STATE_RELEASED;

            auto w = touch_data->swap_xy ? touch_data->driver->height() : touch_data->driver->width();
            auto h = touch_data->swap_xy ? touch_data->driver->width() : touch_data->driver->height();
            auto x = touch_data->swap_xy ? pt.y : pt.x;
            auto y = touch_data->swap_xy ? pt.x : pt.y;
            
            data->point.x = touch_data->invert_x ? w - x : x;
            data->point.y = touch_data->invert_y ? h - y : y;

            data->point.x = LV_CLAMP(0, data->point.x, (int32_t)w);
            data->point.y = LV_CLAMP(0, data->point.y, (int32_t)h);
        });
        
        return touch_indev;
    }
}