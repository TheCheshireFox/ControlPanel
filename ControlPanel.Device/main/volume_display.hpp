#pragma once

#include <functional>
#include <map>
#include <memory>
#include <string>
#include <vector>

#include "lvgl.h"
#include "utils/lv_sync.hpp"

#include "volume_display_model.hpp"
#include "ui/style.hpp"
#include "ui/flex_list.hpp"
#include "ui/list_item.hpp"

class volume_display_t
{
    struct vl_list_item_t
    {
        lv_obj_t* item;
        std::unique_ptr<list_item_t> list_item;
    };

public:
    static constexpr auto TAG = "DISPLAY";

    volume_display_t(int32_t x, int32_t y, int32_t w, int32_t h)
        : _content(create_content(x, y, w, h))
        , _volume_list(_content, app_style::list, app_style::list_item, x, y, w, h)
    {

    }

    void refresh(Iterable<bridge_audio_stream_t> auto&& updated, Iterable<bridge_audio_stream_id_t> auto&& deleted)
    {
        auto deltas = _model.refresh(updated, deleted);
        apply_deltas(deltas);
        emit_missing_icon_requests(deltas);
    }

    void update_icon(const std::string& source, const std::string& agent_id, int32_t icon_hash, uint32_t w, uint32_t h, std::span<const uint8_t> rgb565a8)
    {
        auto deltas = _model.update_icon(source, agent_id, icon_hash, w, h, rgb565a8);
        apply_deltas(deltas);
    }

    std::size_t size() const
    {
        return _model.size();
    }

    template<typename F>
    void on_volume_change(F&& cb)
    {
        _on_volume_changed = std::forward<F>(cb);
    }

    template<typename F>
    void on_mute_change(F&& cb)
    {
        _on_mute_changed = std::forward<F>(cb);
    }

    template<typename F>
    void on_icon_missing(F&& cb)
    {
        _on_icon_missing = std::forward<F>(cb);
    }

    ~volume_display_t()
    {
        std::unique_lock lock{lv_sync};

        lv_obj_delete(_content);
    }

private:
    void apply_deltas(const std::vector<display_delta_t>& deltas)
    {
        std::scoped_lock lock{lv_sync};

        for (const auto& delta: deltas)
        {
            std::visit([this](const auto& value) { apply_delta(value); }, delta);
        }
    }

    void emit_missing_icon_requests(const std::vector<display_delta_t>& deltas)
    {
        if (!_on_icon_missing)
            return;

        for (const auto& delta: deltas)
        {
            if (const auto* missing = std::get_if<display_missing_icon_t>(&delta))
                _on_icon_missing(missing->source, missing->agent_id, missing->icon_hash);
        }
    }

    void apply_delta(const display_add_item_t& delta)
    {
        ESP_LOGD(TAG, "add (%s, %s) %s", delta.id.id.c_str(), delta.id.agent_id.c_str(), delta.title.c_str());

        auto item = _volume_list.add_item();
        auto [it, inserted] = _volume_list_items.emplace(delta.id, vl_list_item_t{ item, std::make_unique<list_item_t>(item) });
        if (!inserted)
        {
            _volume_list.delete_item(item);
            return;
        }

        auto& list_item = it->second.list_item;
        list_item->set_title(delta.title);
        list_item->set_volume(delta.volume);
        list_item->set_mute(delta.mute);
        list_item->on_mute_changed([id = delta.id, this](bool mute) { mute_change(id, mute); });
        list_item->on_volume_changed([id = delta.id, this](int8_t volume) { volume_change(id, volume); });
    }

    void apply_delta(const display_update_item_t& delta)
    {
        auto it = _volume_list_items.find(delta.id);
        if (it == _volume_list_items.end())
            return;

        ESP_LOGD(TAG, "update (%s, %s)", delta.id.id.c_str(), delta.id.agent_id.c_str());

        auto& list_item = it->second.list_item;
        if (delta.title.has_value()) list_item->set_title(*delta.title);
        if (delta.mute.has_value()) list_item->set_mute(*delta.mute);
        if (delta.volume.has_value()) list_item->set_volume(*delta.volume);
    }

    void apply_delta(const display_remove_item_t& delta)
    {
        ESP_LOGD(TAG, "erasing (%s, %s)", delta.id.id.c_str(), delta.id.agent_id.c_str());

        auto it = _volume_list_items.find(delta.id);
        if (it == _volume_list_items.end())
        {
            ESP_LOGW(TAG, "erasing non-existent (%s, %s)", delta.id.id.c_str(), delta.id.agent_id.c_str());
            return;
        }

        auto item = it->second.item;
        _volume_list_items.erase(it);
        if (!_volume_list.delete_item(item))
            ESP_LOGW(TAG, "list item not deleted (%s, %s)", delta.id.id.c_str(), delta.id.agent_id.c_str());
    }

    void apply_delta(const display_set_item_icon_t& delta)
    {
        auto it = _volume_list_items.find(delta.id);
        if (it == _volume_list_items.end())
            return;

        ESP_LOGD(TAG, "update icon for (%s, %s), size=%d", delta.id.id.c_str(), delta.id.agent_id.c_str(), delta.icon.data.size());
        it->second.list_item->set_app_image(LV_COLOR_FORMAT_RGB565A8, delta.icon.w, delta.icon.h, delta.icon.data);
    }

    void apply_delta(const display_missing_icon_t&) {}

    void volume_change(const event_id& id, int8_t value)
    {
        ESP_LOGD(TAG, "%s", "volume_change");
        if (_on_volume_changed)
            _on_volume_changed(id, volume_display_model_t::to_volume_fraction(value));
    }

    void mute_change(const event_id& id, bool mute)
    {
        ESP_LOGD(TAG, "%s", "mute_change");
        if (_on_mute_changed)
            _on_mute_changed(id, mute);
    }

    static lv_obj_t* create_content(int32_t x, int32_t y, int32_t w, int32_t h)
    {
        std::scoped_lock lock{lv_sync};

        auto content = lv_obj_create(lv_scr_act());
        lv_obj_set_pos(content, x, y);
        lv_obj_set_size(content, w, h);
        lv_obj_set_scrollbar_mode(content, LV_SCROLLBAR_MODE_OFF);
        lv_obj_add_style(content, app_style::content, 0);
        lv_obj_update_layout(content);
        return content;
    }

private:
    lv_obj_t* _content;
    flex_list_t _volume_list;
    volume_display_model_t _model;
    std::map<event_id, vl_list_item_t> _volume_list_items;
    std::function<void(const event_id& id, float)> _on_volume_changed;
    std::function<void(const event_id& id, bool)> _on_mute_changed;
    std::function<void(const std::string&, const std::string&, int32_t)> _on_icon_missing;
};