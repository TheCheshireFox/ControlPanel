#pragma once

#include "lvgl.h"
#include "waveshare_st7789.hpp"

namespace waveshare_st7789
{
    static lv_display_t* lvgl_create_display(waveshare_st7789_t* driver)
    {
        const auto hor_res = driver->width();
        const auto ver_res = driver->height();

        auto disp = lv_display_create(hor_res, ver_res);
        lv_display_set_color_format(disp, LV_COLOR_FORMAT_RGB565);
        lv_display_set_user_data(disp, driver);

        lv_display_set_flush_cb(disp, +[](lv_display_t* display, const lv_area_t *area, unsigned char *px_map)
        {
            auto driver = (waveshare_st7789_t*)lv_display_get_user_data(display);

            lv_draw_sw_rgb565_swap(px_map, lv_area_get_size(area));

            driver->draw(area->x1, area->y1, area->x2, area->y2, px_map, lv_area_get_size(area) * LV_COLOR_FORMAT_GET_SIZE(LV_COLOR_FORMAT_RGB565));

            lv_display_flush_ready(display);
        });

        return disp;
    }
}