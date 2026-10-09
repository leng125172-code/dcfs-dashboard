<script setup lang="ts">
import { Moon, Sunny } from '@element-plus/icons-vue'
import { nextTick } from 'vue'
import { useUiStore } from '@/stores/ui'

const uiStore = useUiStore()

function beforeThemeChange() {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (!('startViewTransition' in document) || prefersReducedMotion) {
    return true
  }

  return new Promise<boolean>((resolve) => {
    document.startViewTransition(async () => {
      resolve(true)
      await nextTick()
    })
  })
}

function changeTheme(value: string | number | boolean) {
  uiStore.setTheme(Boolean(value))
}
</script>

<template>
  <div class="theme-switch">
    <el-switch
      :model-value="uiStore.isDark"
      :active-action-icon="Moon"
      :inactive-action-icon="Sunny"
      :before-change="beforeThemeChange"
      aria-label="切换明暗主题"
      @change="changeTheme"
    />
  </div>
</template>
