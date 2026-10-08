<script setup lang="ts">
import { Bell, Search } from '@element-plus/icons-vue'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import ThemeSwitch from '@/components/ThemeSwitch.vue'

withDefaults(
  defineProps<{
    loginMode?: boolean
    showNavigationTrigger?: boolean
  }>(),
  {
    loginMode: false,
    showNavigationTrigger: false,
  },
)

const emit = defineEmits<{
  toggleNavigation: []
}>()

const whaleMarkUrl = '/images/whaledeck-mark.png'
const isScrolled = ref(false)

function updateScrollState() {
  isScrolled.value = window.scrollY > 8
}

function updateTopbarSpotlight(event: PointerEvent) {
  const target = event.currentTarget as HTMLElement
  const rect = target.getBoundingClientRect()
  target.style.setProperty('--topbar-spotlight-x', `${event.clientX - rect.left}px`)
  target.style.setProperty('--topbar-spotlight-y', `${event.clientY - rect.top}px`)
  target.style.setProperty('--topbar-spotlight-opacity', '1')
}

function hideTopbarSpotlight(event: PointerEvent) {
  const target = event.currentTarget as HTMLElement
  target.style.setProperty('--topbar-spotlight-opacity', '0')
}

onMounted(() => {
  updateScrollState()
  window.addEventListener('scroll', updateScrollState, { passive: true })
})

onBeforeUnmount(() => window.removeEventListener('scroll', updateScrollState))
</script>

<template>
  <header
    data-test="topbar"
    class="topbar"
    :class="{ 'topbar--scrolled': isScrolled, 'topbar--login': loginMode }"
    @pointermove="updateTopbarSpotlight"
    @pointerleave="hideTopbarSpotlight"
  >
    <RouterLink class="brand" to="/" aria-label="返回 WhaleDeck 首页">
      <span class="brand__mark brand__mark--image" aria-hidden="true">
        <img :src="whaleMarkUrl" alt="" />
      </span>
      <span class="brand__text">
        <strong>Whale Deck</strong>
      </span>
    </RouterLink>

    <div class="topbar__workspace">
      <el-button
        v-if="showNavigationTrigger"
        class="navigation-trigger navigation-trigger--mobile"
        text
        circle
        aria-label="打开导航"
        @click="emit('toggleNavigation')"
      >
        <span class="navigation-trigger__glyph" aria-hidden="true"><i /><i /><i /></span>
      </el-button>

      <template v-if="!loginMode">
        <button class="command-search" type="button" aria-label="搜索平台功能">
          <el-icon><Search /></el-icon>
          <span>搜索功能</span>
          <kbd>Ctrl K</kbd>
        </button>

        <div class="topbar__actions">
          <span class="connection-state">
            <span class="connection-state__pulse" />
            内部网络
          </span>
          <el-tooltip content="通知" placement="bottom">
            <el-button class="icon-action" text circle aria-label="通知">
              <el-badge is-dot>
                <el-icon><Bell /></el-icon>
              </el-badge>
            </el-button>
          </el-tooltip>
          <ThemeSwitch />
        </div>
      </template>

      <ThemeSwitch v-else />
    </div>
  </header>
</template>
