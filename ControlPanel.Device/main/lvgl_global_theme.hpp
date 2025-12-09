#pragma once

#include "lvgl.h"

static void lvgl_init_global_theme(lv_display_t* disp)
{
    auto col_primary = lv_color_hex(0xFF8800);
    auto col_bg      = lv_color_hex(0x000000);

    auto theme = lv_theme_default_init(disp, col_primary, col_bg, true, LV_FONT_DEFAULT);
    lv_disp_set_theme(disp, theme);
}