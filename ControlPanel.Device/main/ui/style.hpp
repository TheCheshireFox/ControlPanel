#pragma once

#include <vector>

#include "lvgl.h"

consteval lv_color_t color_hex(uint32_t c)
{
    lv_color_t ret;
    ret.red = (c >> 16) & 0xff;
    ret.green = (c >> 8) & 0xff;
    ret.blue = (c >> 0) & 0xff;
    return ret;
}

struct app_style
{
    inline static constexpr lv_color_t primary_fg = color_hex(0xFF8800);
    inline static constexpr lv_color_t primary_bg = color_hex(0x000000);
    
private:
    inline static lv_style_t* init_content_style()
    {
        static lv_style_t s;
        lv_style_init(&s);
        without_borders(&s);
        lv_style_set_pad_top(&s, 8);
        return &s;
    }

    inline static lv_style_t* init_title_style()
    {
        static lv_style_t s;
        lv_style_init(&s);
        lv_style_set_height(&s, 18);
        lv_style_set_pad_all(&s, 0);
        lv_style_set_margin_bottom(&s, 0);
        lv_style_set_recolor(&s, primary_fg);
        lv_style_set_recolor_opa(&s, LV_OPA_COVER);
        lv_style_set_align(&s, LV_ALIGN_LEFT_MID);
        return &s;
    }

    inline static lv_style_t* init_slider_style()
    {
        static lv_style_t s;
        lv_style_init(&s);
        lv_style_set_height(&s, 16);
        lv_style_set_pad_ver(&s, 0);
        return &s;
    }

    inline static lv_style_t* init_slider_label()
    {
        static lv_style_t s;
        lv_style_init(&s);
        lv_style_set_text_align(&s, LV_TEXT_ALIGN_RIGHT);
        lv_style_set_pad_all(&s, 0);
        lv_style_set_min_width(&s, 34);
        return &s;
    }

    inline static lv_style_t* init_list_style()
    {
        static lv_style_t s;
        lv_style_init(&s);

        without_borders(&s);
        lv_style_set_pad_row(&s, 8);
        lv_style_set_pad_column(&s, 0);
        return &s;
    }

    inline static lv_style_t* init_list_item_style()
    {
        static lv_style_t s;
        lv_style_init(&s);

        without_borders(&s);
        lv_style_set_pad_row(&s, 0);
        lv_style_set_pad_column(&s, 4);
        lv_style_set_min_height(&s, LV_SIZE_CONTENT);
        return &s;
    }

    inline static lv_style_t* init_app_icon_img()
    {
        static lv_style_t s;
        lv_style_init(&s);
        lv_style_set_size(&s, 32, 32);
        return &s;
    }

    inline static lv_style_t* init_mute_click_area()
    {
        static lv_style_t s;
        lv_style_init(&s);
        without_borders(&s);
        return &s;
    }

    inline static void without_borders(lv_style_t* s)
    {
        lv_style_set_pad_all(s, 0);
        lv_style_set_margin_all(s, 0);
        lv_style_set_border_width(s, 0);
        lv_style_set_outline_width(s, 0);
        lv_style_set_shadow_width(s, 0);
        lv_style_set_radius(s, 0);
    }

public:
    inline static const lv_style_t* content;
    inline static const lv_style_t* title;
    inline static const lv_style_t* slider;
    inline static const lv_style_t* slider_label;
    inline static const lv_style_t* list;
    inline static const lv_style_t* list_item;
    inline static const lv_style_t* app_icon_img;
    inline static const lv_style_t* mute_click_area;

    static void init(lv_display_t* disp)
    {
        std::scoped_lock lock{lv_sync};

        content = init_content_style();
        title = init_title_style();
        slider = init_slider_style();
        slider_label = init_slider_label();
        list = init_list_style();
        list_item = init_list_item_style();
        app_icon_img = init_app_icon_img();
        mute_click_area = init_mute_click_area();

        auto theme = lv_theme_default_init(disp, primary_fg, primary_bg, true, LV_FONT_DEFAULT);
        lv_disp_set_theme(disp, theme);
    }
};