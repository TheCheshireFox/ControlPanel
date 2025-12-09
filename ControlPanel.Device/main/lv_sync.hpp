#pragma once

#include <mutex>

struct lv_lock_t {
#if LV_USE_OS != LV_OS_NONE
    void lock() { lv_lock(); }
    void unlock() { lv_unlock(); }
#else
    void lock() { _mutex.lock(); }
    void unlock() { _mutex.unlock(); }
    private: std::recursive_mutex _mutex;
#endif
};

struct lv_timer_lock_t {
    lv_timer_lock_t(lv_lock_t& lv_lck) : _lv_lock(lv_lck) {}

#if LV_USE_OS != LV_OS_NONE
    void lock() { }
    void unlock() { }
#else
    void lock() { _lv_lock.lock(); }
    void unlock() { _lv_lock.unlock(); }
#endif

private:
    lv_lock_t& _lv_lock;
};

inline lv_lock_t lv_sync;
inline lv_timer_lock_t lv_timer_sync(lv_sync);