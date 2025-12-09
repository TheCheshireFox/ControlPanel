#pragma once

#include <string>
#include <cstring>
#include <map>
#include <memory>
#include <tuple>
#include <vector>
#include <set>

#include "lvgl.h"
#include "lv_sync.hpp"
#include "lv_memory.hpp"
#include "images/audio_high_16.hpp"
#include "images/audio_muted_16.hpp"

#include "protocol.hpp"

template<typename T>
class ui_obj_base_t
{
    void set_style(this auto&& self, lv_style_t* style, lv_style_selector_t selector)
    {
        lv_obj_add_style(self->get_lv_obj().get(), style, selector);
    }
};

class label_t : public ui_obj_base_t<label_t>
{
public:
    label_t(lv_obj_t* parent, const std::string& text = {})
    {
        _label = make_shared_lv(lv_label_create(parent));
        
        if (!text.empty())
            set_text(text);
    }

    void set_text(const std::string& text)
    {
        lv_label_set_text(_label.get(), text.c_str());
    }

    void on_event(lv_obj_flag_t flag, lv_event_code_t event, std::function<void(label_t&)> cb)
    {
        lv_obj_add_flag(_label.get(), flag);
        lv_obj_add_event_cb(_label.get(), +[](lv_event_t* e)
        {
            auto that = (label_t*)lv_event_get_user_data(e);
            auto code = lv_event_get_code(e);
            if (that->_on_event) that->_on_event(code, *that);
        }, event, this);
    }

protected:
    std::shared_ptr<lv_obj_t> get_lv_obj() const { return _label; }

private:
    std::shared_ptr<lv_obj_t> _label;
    std::function<void(lv_event_code_t, label_t&)> _on_event;
};

class img_t : ui_obj_base_t<img_t>
{
    img_t(lv_obj_t* parent, int w, int h, lv_image_dsc_t* dsc = nullptr)
        : _w(w), _h(h)
    {
        _img = make_shared_lv(lv_img_create(parent));

        if (dsc != nullptr)
            set_image(dsc);
    }

    void set_image(lv_image_dsc_t* dsc)
    {
        configASSERT(dsc);

        lv_img_set_src(_img.get(), dsc);
        _data.clear();
    }

    void set_image(lv_color_format_t format, const std::vector<uint8_t>& data)
    {
        std::scoped_lock lock{lv_sync};

        _data = data;
        std::memset(&_dsc, 0, sizeof(_dsc));

        _dsc.header.magic= LV_IMAGE_HEADER_MAGIC;
        _dsc.header.cf = format;
        _dsc.header.w  = _w;
        _dsc.header.h  = _h;
        _dsc.data_size = _data.size();
        _dsc.data = _data.data();

        lv_img_set_src(_img.get(), &_dsc);
    }

    void on_event(lv_obj_flag_t flag, lv_event_code_t event, std::function<void(label_t&)> cb)
    {
        lv_obj_add_flag(_img.get(), flag);
        lv_obj_add_event_cb(_img.get(), +[](lv_event_t* e)
        {
            auto that = (img_t*)lv_event_get_user_data(e);
            auto code = lv_event_get_code(e);
            if (that->_on_event) that->_on_event(code, *that);
        }, event, this);
    }

protected:
    std::shared_ptr<lv_obj_t> get_lv_obj() const { return _img; }

private:
    int _w, _h;
    std::shared_ptr<lv_obj_t> _img;
    lv_image_dsc_t _dsc;
    std::vector<uint8_t> _data;
    std::function<void(lv_event_code_t, img_t&)> _on_event;
};

class slider_t : ui_obj_base_t<slider_t>
{
public:
    slider_t(lv_obj_t* parent, int32_t current, int32_t min, int32_t max)
    {
        _slider = make_shared_lv(lv_slider_create(parent));
        
        lv_slider_set_range(_slider.get(), min, max);
        lv_slider_set_value(_slider.get(), current, LV_ANIM_OFF);

        _value_label = make_shared_lv(lv_label_create(parent));
        lv_label_set_text(_value_label.get(), std::to_string(current).c_str());
    }

    void on_event(lv_event_code_t event, std::function<void(label_t&)> cb)
    {
        lv_obj_add_event_cb(_slider.get(), +[](lv_event_t* e)
        {
            auto that = (slider_t*)lv_event_get_user_data(e);
            auto code = lv_event_get_code(e);
            if (that->_on_event) that->_on_event(code, *that);
        }, event, this);
    }

protected:
    std::shared_ptr<lv_obj_t> get_lv_obj() const { return _label; }

private:
    std::shared_ptr<lv_obj_t> _slider;
    std::shared_ptr<lv_obj_t> _value_label;
    std::function<void(lv_event_code_t, slider_t&)> _on_event;
};

struct event_id
{
    std::string id;
    std::string agent_id;

    bool operator==(const event_id& other) const { return std::tuple{id, agent_id} == std::tuple{other.id, other.agent_id}; }
    bool operator<(const event_id& other) const { return std::tuple{id, agent_id} < std::tuple{other.id, other.agent_id}; }
};

class volume_display_t
{
    static constexpr const char* TAG = "DISPLAY";

    template<typename T>
    struct event_context_t
    {
        event_id id;
        volume_display_t* that;
        T data;

        event_context_t(const event_id& id, volume_display_t* that, T data)
            : id(id), that(that), data(data)
        {

        }
    };

    struct slider_context_data_t
    {
        lv_obj_t* label;
        bool user_editing;
    };

    struct title_context_data_t
    {
        std::shared_ptr<lv_img_dsc_t> icon;
        bool mute;
    };

    struct list_item_t
    {
        lv_obj_t* title;
        lv_obj_t* mute;
        lv_obj_t* volume;
        lv_obj_t* volume_label;
        lv_obj_t* app_icon;
        std::shared_ptr<event_context_t<title_context_data_t>> title_ctx;
        std::shared_ptr<event_context_t<slider_context_data_t>> slider_ctx;
        std::shared_ptr<lv_obj_t> item;
    };

public:
    volume_display_t(int x, int y, int w, int h)
    {
        std::scoped_lock lock{lv_sync};

        _content = lv_obj_create(lv_scr_act());
        lv_obj_set_pos(_content, x, y);
        lv_obj_set_size(_content, w, h);
        lv_obj_set_scrollbar_mode(_content, LV_SCROLLBAR_MODE_OFF);
        lv_obj_update_layout(_content);

        _volume_list = create_list(_content);
        lv_obj_update_layout(_volume_list);
    }

    void refresh(std::vector<bridge_audio_stream_t>& streams)
    {
        std::scoped_lock lock{lv_sync};

        remove_outdated(streams);

        std::sort(streams.begin(), streams.end(), _sort_comp);

        for (const auto& stream: streams)
        {
            event_id id{stream.id, stream.agent_id};

            auto it = _volume_list_items.find(id);
            if (it == _volume_list_items.end())
            {
                add_item(id, stream.name, stream.volume, stream.mute);
            }
            else
            {
                auto& item = it->second;

                lv_label_set_text(item.title, stream.name.c_str());
                lv_img_set_src(item.mute, stream.mute ? &audio_muted_16 : &audio_high_16);
                item.title_ctx->data.mute = stream.mute;
                
                if (!item.slider_ctx->data.user_editing)
                {
                    lv_slider_set_value(item.volume, (int32_t)(stream.volume * 100), LV_ANIM_OFF);
                    lv_label_set_text(item.volume_label, std::to_string((int32_t)(stream.volume * 100)).c_str());
                }
            }
        }
    }

    void update_icon(const event_id& id, const std::vector<uint8_t>& rgb565a8)
    {
        std::scoped_lock lock{lv_sync};
        auto icon_it = _icons_cache.emplace(id, rgb565a8).first;

        auto it = _volume_list_items.find(id);
        if (it == _volume_list_items.end())
        {
            ESP_LOGE(TAG, "update icon for non-existing item (%s, %s)", id.id.c_str(), id.agent_id.c_str());
            return;
        }

        it->second.title_ctx->data.icon = create_lv_img_dsc(icon_it->second);
        lv_img_set_src(it->second.app_icon, it->second.title_ctx->data.icon.get());
    }

    void register_on_volume_change(std::function<void(const event_id& id, float)> cb)
    {
        _on_volume_changed = std::move(cb);
    }

    void register_on_mute_change(std::function<void(const event_id& id, bool)> cb)
    {
        _on_mute_changed = std::move(cb);
    }

    void register_on_icon_missing(std::function<void(const event_id& id)> cb)
    {
        _on_icon_missing = std::move(cb);
    }

private:
    void volume_change(const event_id& id, int32_t value)
    {
        if (_on_volume_changed)
            _on_volume_changed(id, value / 100.0f);
    }

    void mute_change(const event_id& id, bool mute)
    {
        if (_on_mute_changed)
            _on_mute_changed(id, mute);
    }

    void remove_outdated(const std::vector<bridge_audio_stream_t>& streams)
    {
        std::set<event_id> ids;
        for (auto const& [k, _] : _volume_list_items)
            ids.insert(k);

        std::set<event_id> streams_ids_set;
        for (auto const& s : streams)
            streams_ids_set.insert({s.id, s.agent_id});

        std::vector<event_id> to_remove;
        std::set_difference(ids.begin(), ids.end(), streams_ids_set.begin(), streams_ids_set.end(), std::inserter(to_remove, to_remove.begin()));

        for (const auto& id: to_remove)
        {
            _volume_list_items.erase(id);
        }
    }

    lv_obj_t* create_list(lv_obj_t* parent)
    {
        auto list = lv_obj_create(parent);
        lv_obj_set_pos(list, 0, 0);
        lv_obj_set_size(list, LV_PCT(100), LV_PCT(100));
        lv_obj_set_scroll_dir(list, lv_dir_t::LV_DIR_VER);
        lv_obj_set_scrollbar_mode(list, LV_SCROLLBAR_MODE_AUTO);

        lv_obj_set_flex_flow(list, LV_FLEX_FLOW_COLUMN);
        lv_obj_set_flex_align(list, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_CENTER);

        lv_obj_set_style_pad_top(list, 0, 0);
        lv_obj_set_style_pad_bottom(list, 0, 0);
        lv_obj_set_style_pad_left(list, 4, 0);
        lv_obj_set_style_pad_right(list, 4, 0);
        lv_obj_set_style_pad_row(list, 2, 0);      // vertical gap between items
        lv_obj_set_style_pad_column(list, 0, 0);   // not used in column flow

        lv_obj_set_style_border_width(list, 0, 0);
        lv_obj_set_style_outline_width(list, 0, 0);
        lv_obj_set_style_shadow_width(list, 0, 0);

        return list;
    }

    auto create_title_row(const event_id& id, lv_obj_t* item, const std::string& title, bool mute)
    {
        title_context_data_t ctx_data {
            .mute = mute
        };
        
        auto ctx = std::make_shared<event_context_t<title_context_data_t>>(id, this, ctx_data);

        auto app_icon = create_app_icon(id, item, ctx->data);
        auto text = create_app_title(item, title, ctx);
        auto icon = create_mute_icon(item, mute, ctx);

        return std::tuple{text, icon, app_icon, ctx};
    }

    lv_obj_t* create_app_icon(const event_id& id, lv_obj_t* item, title_context_data_t& ctx_data)
    {
        auto app_icon = lv_img_create(item);
        lv_obj_set_size(app_icon, 18, 18);
        lv_obj_set_grid_cell(app_icon, LV_GRID_ALIGN_CENTER, 0, 1, LV_GRID_ALIGN_END, 0, 1);

        auto it = _icons_cache.find(id);
        if (it == _icons_cache.end())
        {
            if (_on_icon_missing)
                _on_icon_missing(id);
        }
        else
        {
            ctx_data.icon = create_lv_img_dsc(it->second);
            lv_img_set_src(app_icon, ctx_data.icon.get());
        }

        return app_icon;
    }

    std::shared_ptr<lv_img_dsc_t> create_lv_img_dsc(const std::vector<uint8_t>& rgb565a8)
    {
        auto dsc = make_shared_lv_alloc<lv_img_dsc_t>();
        std::memset(dsc.get(), 0, sizeof(lv_img_dsc_t));

        dsc->header.magic= LV_IMAGE_HEADER_MAGIC;
        dsc->header.cf = LV_COLOR_FORMAT_RGB565A8;
        dsc->header.w  = 18;
        dsc->header.h  = 18;
        dsc->data_size = rgb565a8.size();
        dsc->data = rgb565a8.data();

        return dsc;
    }

    lv_obj_t* create_app_title(lv_obj_t* item, const std::string& title, std::shared_ptr<event_context_t<title_context_data_t>> ctx)
    {
        auto text = lv_label_create(item);
        lv_label_set_text(text, title.c_str());
        lv_obj_set_grid_cell(text, LV_GRID_ALIGN_STRETCH, 1, 1, LV_GRID_ALIGN_STRETCH, 0, 1);

        lv_obj_set_style_pad_all(text, 0, 0);
        lv_obj_set_style_pad_bottom(text, 4, 0);

        lv_obj_add_flag(text, LV_OBJ_FLAG_CLICKABLE);
        lv_obj_add_event_cb(text, img_event_cb, LV_EVENT_CLICKED, ctx.get());

        return text;
    }

    lv_obj_t* create_mute_icon(lv_obj_t* item, bool mute, std::shared_ptr<event_context_t<title_context_data_t>> ctx)
    {
        auto icon = lv_img_create(item);
        lv_obj_set_size(icon, 18, 18);
        lv_img_set_src(icon, mute ? &audio_muted_16 : &audio_high_16);
        lv_obj_set_grid_cell(icon, LV_GRID_ALIGN_CENTER, 2, 1, LV_GRID_ALIGN_END, 0, 1);

        lv_obj_add_flag(icon, LV_OBJ_FLAG_CLICKABLE);
        lv_obj_set_ext_click_area(icon, 6);
        lv_obj_add_event_cb(icon, img_event_cb, LV_EVENT_CLICKED, ctx.get());

        return icon;
    }

    auto create_slider_row(const event_id& id, lv_obj_t* item, float volume)
    {
        auto slider_value = (int32_t)(volume * 100);

        auto ctx = std::make_shared<event_context_t<slider_context_data_t>>(id, this, slider_context_data_t{});

        auto slider = create_slider(item, slider_value, ctx);
        auto value_label = create_slider_label(item, slider_value);

        ctx->data.label = value_label;

        return std::tuple{slider, value_label, ctx};
    }

    lv_obj_t* create_slider(lv_obj_t* item, int32_t volume, std::shared_ptr<event_context_t<slider_context_data_t>> ctx)
    {
        auto slider = lv_slider_create(item);
        lv_slider_set_range(slider, 0, 100);
        lv_slider_set_value(slider, volume, LV_ANIM_OFF);
        lv_obj_set_grid_cell(slider, LV_GRID_ALIGN_STRETCH, 0, 2, LV_GRID_ALIGN_CENTER, 1, 1);

        lv_obj_add_event_cb(slider, slider_event_cb, LV_EVENT_ALL, ctx.get());

        // Force a compact slider inside the flex row/column
        lv_obj_set_style_height(slider, 16, LV_PART_MAIN | LV_PART_INDICATOR | LV_PART_KNOB);
        lv_obj_set_style_pad_ver(slider, 0, LV_PART_MAIN | LV_PART_INDICATOR | LV_PART_KNOB);

        return slider;
    }

    lv_obj_t* create_slider_label(lv_obj_t* item, int32_t volume)
    {
        auto value_label = lv_label_create(item);
        lv_label_set_text(value_label, std::to_string(volume).c_str());
        lv_obj_set_style_text_align(value_label, LV_TEXT_ALIGN_RIGHT, 0);
        lv_obj_set_grid_cell(value_label, LV_GRID_ALIGN_END, 1, 1, LV_GRID_ALIGN_CENTER, 1, 1);

        return value_label;
    }

    lv_obj_t* add_item(const event_id& id, const std::string& title, float volume, bool mute)
    {
        auto item = lv_obj_create(_volume_list);
        lv_obj_set_width(item, LV_PCT(100));
        lv_obj_set_height(item, LV_SIZE_CONTENT);
        lv_obj_set_scroll_dir(item, lv_dir_t::LV_DIR_NONE);
        lv_obj_set_scrollbar_mode(item, LV_SCROLLBAR_MODE_OFF);

        static int32_t col_dsc[] = { LV_GRID_CONTENT, LV_GRID_FR(1), LV_GRID_CONTENT, LV_GRID_TEMPLATE_LAST };
        static int32_t row_dsc[] = { LV_GRID_CONTENT, LV_GRID_CONTENT, LV_GRID_TEMPLATE_LAST };

        lv_obj_set_grid_dsc_array(item, col_dsc, row_dsc);
        lv_obj_set_layout(item, LV_LAYOUT_GRID);

        // kill “card” look
        lv_obj_set_style_pad_all(item, 2, 0);
        lv_obj_set_style_pad_row(item, 0, 0);
        lv_obj_set_style_pad_column(item, 4, 0);
        lv_obj_set_style_border_width(item, 0, 0);
        lv_obj_set_style_outline_width(item, 0, 0);
        lv_obj_set_style_shadow_width(item, 0, 0);
        lv_obj_set_style_radius(item, 0, 0);

        // shrink height so flex doesn’t add extra space
        lv_obj_set_style_min_height(item, LV_SIZE_CONTENT, 0);

        auto [text, icon, app_icon, title_ctx] = create_title_row(id, item, title, mute);
        auto [slider, value_label, slider_ctx] = create_slider_row(id, item, volume);

        _volume_list_items.emplace(id, list_item_t {
            .title = text,
            .mute = icon,
            .volume = slider,
            .volume_label = value_label,
            .app_icon = app_icon,
            .title_ctx = title_ctx,
            .slider_ctx = slider_ctx,
            .item = make_shared_lv(item),
        });

        return item;
    }

    template<typename T>
    static auto extract_context(lv_event_t* e)
    {
        std::scoped_lock lock{lv_sync};

        auto target = (lv_obj_t*)lv_event_get_target(e);
        auto ctx = (event_context_t<T>*)lv_event_get_user_data(e);
        configASSERT(target && ctx);

        return std::tuple{target, ctx};
    }

    static void slider_event_cb(lv_event_t* e)
    {
        auto [slider, ctx] = extract_context<slider_context_data_t>(e);
        auto code = lv_event_get_code(e);

        switch (code)
        {
            case LV_EVENT_PRESSED:
            case LV_EVENT_PRESSING:
            {
                std::scoped_lock lock{lv_sync};
                
                ctx->data.user_editing = true;
                break;
            }
            case LV_EVENT_RELEASED:
            {
                int32_t value;
                {
                    std::scoped_lock lock{lv_sync};
                    
                    value = lv_slider_get_value(slider);
                    ctx->data.user_editing = false;
                }

                ctx->that->volume_change(ctx->id, value);
                break;
            }
            case LV_EVENT_VALUE_CHANGED:
            {
                std::scoped_lock lock{lv_sync};

                auto value = lv_slider_get_value(slider);
                lv_label_set_text(ctx->data.label, std::to_string(value).c_str());
                break;
            }
            default:
                break;
        }
    }

    static void img_event_cb(lv_event_t* e)
    {
        auto [img, ctx] = extract_context<title_context_data_t>(e);
        ctx->that->mute_change(ctx->id, !ctx->data.mute);
    }

private:
    lv_obj_t* _content;
    lv_obj_t* _volume_list;
    std::map<event_id, list_item_t> _volume_list_items;
    std::map<event_id, std::vector<u_int8_t>> _icons_cache;
    std::function<void(const event_id& id, float)> _on_volume_changed;
    std::function<void(const event_id& id, bool)> _on_mute_changed;
    std::function<void(const event_id& id)> _on_icon_missing;

    inline static auto _sort_comp = +[](bridge_audio_stream_t x, bridge_audio_stream_t y){ return x.name < y.name; };
};