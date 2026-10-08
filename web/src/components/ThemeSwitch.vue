<script setup lang="ts">
import { Moon, Sunny } from '@element-plus/icons-vue'
import { ref } from 'vue'
import { useUiStore } from '@/stores/ui'

const uiStore = useUiStore()
const burst = ref({ active: false, x: 0, y: 0, key: 0 })
let pointerPosition: { x: number; y: number } | null = null

function rememberPointer(event: PointerEvent) {
  pointerPosition = { x: event.clientX, y: event.clientY }
}

function changeTheme(value: string | number | boolean) {
  const fallback = { x: window.innerWidth - 44, y: 32 }
  const position = pointerPosition ?? fallback

  burst.value = {
    active: true,
    x: position.x,
    y: position.y,
    key: burst.value.key + 1,
  }
  uiStore.setTheme(Boolean(value))
  pointerPosition = null

  window.setTimeout(() => {
    burst.value.active = false
  }, 560)
}
</script>

<template>
  <div class="theme-switch" @pointerdown="rememberPointer">
    <el-switch
      :model-value="uiStore.isDark"
      :active-action-icon="Moon"
      :inactive-action-icon="Sunny"
      aria-label="切换明暗主题"
      @change="changeTheme"
    />
  </div>
  <Teleport to="body">
    <span
      v-if="burst.active"
      :key="burst.key"
      class="theme-burst"
      :style="{ '--theme-x': `${burst.x}px`, '--theme-y': `${burst.y}px` }"
      aria-hidden="true"
    />
  </Teleport>
</template>
