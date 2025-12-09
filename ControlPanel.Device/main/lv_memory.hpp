#pragma once

#include <memory>

#include "lvgl.h"

template<typename T>
inline std::shared_ptr<T> make_shared_lv(T* obj)
{
    return std::shared_ptr<T>(obj, +[](T* o) {
        if (!o) return;
        std::scoped_lock lock{lv_sync};
        lv_obj_delete(o);
    });
}

template<typename T>
inline std::shared_ptr<T> make_shared_lv_alloc()
{
    return std::shared_ptr<T>((T*)lv_malloc(sizeof(T)), +[](T* o) {
        if (!o) return;
        lv_free(o);
    });
}