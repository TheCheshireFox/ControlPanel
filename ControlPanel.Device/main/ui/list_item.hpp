#pragma once

#include <string>
#include <memory>
#include <functional>

#include "ui/style.hpp"

#include "lvgl.h"

#include "utils/esp_utility.hpp"

class list_item_t
{
public:
    list_item_t(lv_obj_t* parent)
        : _mute(false), _slider_editing(false), _on_volume_changed(), _on_mute_changed()
    {
        std::unique_lock lock{lv_sync};

        _app_icon = {parent};
        _mute_click_area = lv_obj_create(parent);
        _title = {parent};
        _slider = lv_slider_create(parent);
        _slider_label = lv_label_create(parent);

        lv_obj_add_style(_app_icon.img, app_style::app_icon_img, 0);
        lv_obj_add_style(_mute_click_area, app_style::mute_click_area, 0);
        lv_obj_add_style(_title.img, app_style::title, 0);
        lv_obj_add_style(_slider, app_style::slider, LV_PART_MAIN | LV_PART_INDICATOR | LV_PART_KNOB);
        lv_obj_add_style(_slider_label, app_style::slider_label, 0);

        lv_image_set_inner_align(_title.img, LV_IMAGE_ALIGN_TOP_LEFT);

        lv_label_set_long_mode(_slider_label, LV_LABEL_LONG_MODE_CLIP);

        lv_slider_set_range(_slider, 0, 100);

        lv_obj_add_flag(_app_icon.img, LV_OBJ_FLAG_CLICKABLE);
        lv_obj_add_flag(_mute_click_area, LV_OBJ_FLAG_CLICKABLE);
        lv_obj_add_flag(_title.img, LV_OBJ_FLAG_CLICKABLE);
        lv_obj_add_flag(_slider_label, LV_OBJ_FLAG_CLICKABLE);
        
        lv_obj_add_event_cb(_slider, +[](lv_event_t* e)
        {
            auto slider = (lv_obj_t*)lv_event_get_target(e);
            auto label = (lv_obj_t*)lv_event_get_user_data(e);
            update_slider_label(slider, label);
        }, LV_EVENT_VALUE_CHANGED, _slider_label);

        lv_obj_add_event_cb(_slider, on_volume_changed_raw, LV_EVENT_PRESSED, this);
        lv_obj_add_event_cb(_slider, on_volume_changed_raw, LV_EVENT_PRESSING, this);
        lv_obj_add_event_cb(_slider, on_volume_changed_raw, LV_EVENT_RELEASED, this);
        lv_obj_add_event_cb(_app_icon.img, on_mute_click_raw, LV_EVENT_CLICKED, this);
        lv_obj_add_event_cb(_mute_click_area, on_mute_click_raw, LV_EVENT_CLICKED, this);

        set_grid_layout(parent);
    }

    void set_app_image(lv_color_format_t format, uint32_t w, uint32_t h, std::span<const uint8_t> data)
    {
        _app_icon.set(format, w, h, data);
    }

    void set_title(lv_color_format_t format, uint32_t w, uint32_t h, std::span<const uint8_t> data)
    {
        _title.set(format, w, h, data);
    }

    void set_mute(bool mute)
    {
        std::scoped_lock lock{lv_sync};

        _mute = mute;
        if (_mute)
        {
            lv_label_set_text(_slider_label, "M");
        }
        else
        {
            update_slider_label(_slider, _slider_label);
        }
    }

    void set_volume(int32_t value)
    {
        std::scoped_lock lock{lv_sync};
        
        if (_slider_editing)
            return;

        lv_slider_set_value(_slider, value, LV_ANIM_OFF);
        lv_label_set_text(_slider_label, std::to_string(value).c_str());
    }

    void on_volume_changed(const std::function<void(int32_t)>& cb)
    {
        _on_volume_changed = cb;
    }

    void on_mute_changed(const std::function<void(bool)>& cb)
    {
        _on_mute_changed = cb;
    }

private:
    static void update_slider_label(lv_obj_t* slider, lv_obj_t* label)
    {
        auto value = lv_slider_get_value(slider);
        lv_label_set_text(label, std::to_string(value).c_str());
    }

    void set_grid_layout(lv_obj_t* list_item)
    {
        static const int32_t cols[] = { 10, LV_GRID_CONTENT, LV_GRID_FR(1), LV_GRID_CONTENT, 10, LV_GRID_TEMPLATE_LAST }; 
        static const int32_t rows[] = { LV_GRID_CONTENT, LV_GRID_CONTENT, LV_GRID_TEMPLATE_LAST }; 

        lv_obj_set_grid_dsc_array(list_item, cols, rows);
        lv_obj_set_layout(list_item, LV_LAYOUT_GRID);

        /*
              0         1        2      3       4
          +-------+----------+-------+-----+---------+
        0 | click | app icon |    title    | <empty> |
          |       |          +-------+-----+         |
        1 |       |          | slider|label|         |
          +-------+----------+-------+-----+---------+
        */

        // row 0
        lv_obj_set_grid_cell(_mute_click_area, LV_GRID_ALIGN_STRETCH, 0, 1, LV_GRID_ALIGN_STRETCH, 0, 2);
        lv_obj_set_grid_cell(_app_icon.img, LV_GRID_ALIGN_STRETCH, 1, 1, LV_GRID_ALIGN_STRETCH, 0, 2);
        lv_obj_set_grid_cell(_title.img, LV_GRID_ALIGN_STRETCH, 2, 2, LV_GRID_ALIGN_CENTER, 0, 1);
        
        // row 1
        lv_obj_set_grid_cell(_slider, LV_GRID_ALIGN_STRETCH, 2, 1, LV_GRID_ALIGN_CENTER, 1, 1);
        lv_obj_set_grid_cell(_slider_label, LV_GRID_ALIGN_STRETCH, 3, 1, LV_GRID_ALIGN_END, 1, 1);
    }

    static void on_mute_click_raw(lv_event_t* e)
    {
        auto that = (list_item_t*)lv_event_get_user_data(e);

        configASSERT(that);

        if (that->_on_mute_changed)
            that->_on_mute_changed(!that->_mute);
    }

    static void on_volume_changed_raw(lv_event_t* e)
    {
        auto slider = (lv_obj_t*)lv_event_get_target_obj(e);
        auto that = (list_item_t*)lv_event_get_user_data(e);
        auto code = lv_event_get_code(e);

        configASSERT(slider && that);
        
        switch (code)
        {
            case LV_EVENT_PRESSED:
            case LV_EVENT_PRESSING:
            {
                that->_slider_editing = true;
                break;
            }
            case LV_EVENT_RELEASED:
            {
                that->_slider_editing = false;
                auto value = lv_slider_get_value(slider);

                if (that->_mute && that->_on_mute_changed)
                    that->_on_mute_changed(false);

                if (that->_on_volume_changed)
                    that->_on_volume_changed(value);
                    
                break;
            }
            default:
                break;
        }
    }

    static lv_obj_t* create_title_viewport(lv_obj_t* parent)
    {
        auto viewport = lv_obj_create(parent);
        lv_obj_add_flag(viewport, LV_OBJ_FLAG_SCROLL_CHAIN_HOR);
        lv_obj_set_scrollbar_mode(viewport, LV_SCROLLBAR_MODE_OFF);
        lv_obj_clear_flag(viewport, LV_OBJ_FLAG_SCROLLABLE);
        return viewport;
    }

private:
    struct image_t
    {
        image_t(lv_obj_t* parent = nullptr) : img(parent != nullptr ? lv_img_create(parent) : nullptr) {}

        void set(lv_color_format_t format, uint32_t w, uint32_t h, std::span<const uint8_t> img_data)
        {
            std::unique_lock lock{lv_sync};

            if (img_data.empty())
            {
                lv_img_set_src(img, nullptr);
                free_data();
                dsc = {};
                return;
            }

            free_data();
            data = (uint8_t*)lv_malloc(img_data.size());
            std::memcpy(data, img_data.data(), img_data.size());
            
            dsc = lv_image_dsc_t
            {
                .header = {
                    .magic= LV_IMAGE_HEADER_MAGIC,
                    .cf = (uint8_t)format,
                    .w = w,
                    .h = h
                },
                .data_size = img_data.size(),
                .data = data
            };

            lv_img_set_src(img, &dsc);
        }

        void free_data()
        {
            if (!data) return;
            lv_free(data);
            data = nullptr;
        }

        ~image_t()
        {
            std::unique_lock lock{lv_sync};

            lv_img_set_src(img, nullptr);
            free_data();
        }

    public:
        lv_obj_t* img;
    
    private:
        lv_image_dsc_t dsc = {};
        uint8_t* data = nullptr;
    };

private:
    lv_obj_t* _mute_click_area;
    image_t _app_icon;
    image_t _title;
    lv_obj_t* _slider;
    lv_obj_t* _slider_label;

    bool _mute;
    bool _slider_editing;
    std::function<void(int32_t)> _on_volume_changed;
    std::function<void(bool)> _on_mute_changed;
};