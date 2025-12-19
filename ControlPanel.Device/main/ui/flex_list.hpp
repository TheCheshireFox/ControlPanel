#pragma once

#include <set>
#include <functional>

#include "lvgl.h"

#include "esp_utility.hpp"

class flex_list_t
{
public:
    flex_list_t(lv_obj_t* parent, const lv_style_t* style, const lv_style_t* item_style, int32_t x, int32_t y, int32_t w, int32_t h)
    {
        std::scoped_lock lock{lv_sync};

        _list = lv_obj_create(parent);
        lv_obj_set_pos(_list, x, y);
        lv_obj_set_size(_list, w, h);
        lv_obj_set_scroll_dir(_list, lv_dir_t::LV_DIR_VER);
        lv_obj_set_scrollbar_mode(_list, LV_SCROLLBAR_MODE_AUTO);

        lv_obj_set_flex_flow(_list, LV_FLEX_FLOW_COLUMN);
        lv_obj_set_flex_align(_list, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_CENTER);

        _item_style = item_style;
        lv_obj_add_style(_list, style, 0);
    }

    lv_obj_t* add_item()
    {
        std::scoped_lock lock{lv_sync};

        auto item = lv_obj_create(_list);
        lv_obj_set_width(item, LV_PCT(100));
        lv_obj_set_height(item, LV_SIZE_CONTENT);
        lv_obj_set_scroll_dir(item, lv_dir_t::LV_DIR_NONE);
        lv_obj_set_scrollbar_mode(item, LV_SCROLLBAR_MODE_OFF);

        lv_obj_add_style(item, _item_style, 0);

        _items.insert(item);
        return item;
    }

    bool delete_item(lv_obj_t* item)
    {
        std::scoped_lock lock{lv_sync};

        auto deleted = _items.erase(item) > 0;
        if (deleted) lv_obj_delete(item);
        return deleted;
    }

private:
    lv_obj_t* _list;
    std::set<lv_obj_t*> _items;
    const lv_style_t* _item_style;
};