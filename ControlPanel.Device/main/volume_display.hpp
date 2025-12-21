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

#include "esp_utility.hpp"

#include "protocol.hpp"
#include "ui/style.hpp"
#include "ui/flex_list.hpp"
#include "ui/list_item.hpp"

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

    struct vl_list_item_t
    {
        lv_obj_t* item;
        std::unique_ptr<list_item_t> list_item;
        std::string source;
    };

public:
    volume_display_t(int32_t x, int32_t y, int32_t w, int32_t h)
        : _content(create_content(x, y, w, h))
        , _volume_list(_content, app_style::list, app_style::list_item, x, y, w, h)
    {

    }

    void refresh(const std::vector<bridge_audio_stream_t>& updated, const std::vector<bridge_audio_stream_id_t>& deleted)
    {
        std::scoped_lock lock{lv_sync};

        remove_outdated(deleted);

        for (const auto& stream: updated)
        {
            event_id id{stream.id.id, stream.id.agent_id};

            auto it = _volume_list_items.find(id);
            if (it == _volume_list_items.end())
            {
                ESP_LOGD(TAG, "add (%s, %s) name_sz=%d", id.id.c_str(), id.agent_id.c_str(), stream.name ? stream.name->sprite.size() : 0);
                if (stream.name && stream.volume && stream.mute)
                {
                    add_item(id, stream.source, *stream.name, *stream.volume, *stream.mute);
                }
                else
                {
                    ESP_LOGE(TAG, "new stream missing information");
                }

                continue;
            }

            auto& list_item = it->second.list_item;

            ESP_LOGD(TAG, "update (%s, %s)", id.id.c_str(), id.agent_id.c_str());

            if (stream.name) list_item->set_title(LV_COLOR_FORMAT_A8, stream.name->width, stream.name->height, stream.name->sprite);
            if (stream.mute) list_item->set_mute(*stream.mute);
            if (stream.volume) list_item->set_volume((int32_t)(*stream.volume * 100));
        }
    }

    void update_icon(const std::string& source, const std::string& agent_id, uint32_t w, uint32_t h, std::span<const uint8_t> rgb565a8)
    {
        std::scoped_lock lock{lv_sync};

        for (auto& [id, vl] : _volume_list_items)
        {
            if (id.agent_id != agent_id || vl.source != source)
                continue;
            
            ESP_LOGD(TAG, "update icon for (%s, %s), size=%d", id.id.c_str(), id.agent_id.c_str(), rgb565a8.size());
            vl.list_item->set_app_image(LV_COLOR_FORMAT_RGB565A8, w, h, rgb565a8);
        }
    }

    std::size_t size() const
    {
        return _volume_list_items.size();
    }

    template<typename F>
    void on_volume_change(F&& cb)
    {
        _on_volume_changed = std::move(cb);
    }

    template<typename F>
    void on_mute_change(F&& cb)
    {
        _on_mute_changed = std::move(cb);
    }

    template<typename F>
    void on_icon_missing(F&& cb)
    {
        _on_icon_missing = std::move(cb);
    }

    ~volume_display_t()
    {
        std::unique_lock lock{lv_sync};

        lv_obj_delete(_content);
    }

private:
    void volume_change(const event_id& id, int32_t value)
    {
        ESP_LOGD(TAG, "%s", "volume_change");
        if (_on_volume_changed)
            _on_volume_changed(id, value / 100.0f);
    }

    void mute_change(const event_id& id, bool mute)
    {
        ESP_LOGD(TAG, "%s", "mute_change");
        if (_on_mute_changed)
            _on_mute_changed(id, mute);
    }

    void remove_outdated(const std::vector<bridge_audio_stream_id_t>& deleted)
    {
        for (const auto& stream_id: deleted)
        {
            event_id id{stream_id.id, stream_id.agent_id};

            ESP_LOGD(TAG, "erasing (%s, %s)", id.id.c_str(), id.agent_id.c_str());

            auto it = _volume_list_items.find(id);
            if (it == _volume_list_items.end())
            {
                ESP_LOGW(TAG, "erasing non-existent (%s, %s)", id.id.c_str(), id.agent_id.c_str());
                continue;
            }

            auto item = it->second.item;
            
            _volume_list_items.erase(it);
            if (!_volume_list.delete_item(item))
            {
                ESP_LOGW(TAG, "list item not deleted (%s, %s)", id.id.c_str(), id.agent_id.c_str());
                continue;
            }
        }
    }

    void add_item(const event_id& id, const std::string& source, const name_sprite_t& title, float volume, bool mute)
    {
        auto item = _volume_list.add_item();
        auto [it, inserted] = _volume_list_items.emplace(id, vl_list_item_t{ item, std::make_unique<list_item_t>(item), source });
        if (!inserted)
            return;
        
        auto& list_item = it->second.list_item;

        list_item->set_title(LV_COLOR_FORMAT_A8, title.width, title.height, title.sprite);
        list_item->set_volume((int32_t)(volume * 100));
        list_item->set_mute(mute);
        list_item->on_mute_changed([id, this](bool mute) { mute_change(id, mute); });
        list_item->on_volume_changed([id, this](int32_t volume) { volume_change(id, volume); });

        if (_on_icon_missing)
            _on_icon_missing(source, id.agent_id);
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
    std::map<event_id, vl_list_item_t> _volume_list_items;
    std::function<void(const event_id& id, float)> _on_volume_changed;
    std::function<void(const event_id& id, bool)> _on_mute_changed;
    std::function<void(const std::string&, const std::string&)> _on_icon_missing;
};