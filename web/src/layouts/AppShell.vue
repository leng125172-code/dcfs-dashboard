<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { Moon, Sunny, UserFilled } from '@element-plus/icons-vue'
import { RouterView, useRoute } from 'vue-router'
import { runtimeConfig } from '@/config/runtime'
import { useUiStore } from '@/stores/ui'

const route = useRoute()
const uiStore = useUiStore()
const isScrolled = ref(false)
const activePath = computed(() => route.path)

function updateScrollState() {
  isScrolled.value = window.scrollY > 10
}

function login() {
  window.location.assign('/api/v1/auth/login')
}

onMounted(() => {
  updateScrollState()
  window.addEventListener('scroll', updateScrollState, { passive: true })
})

onBeforeUnmount(() => window.removeEventListener('scroll', updateScrollState))
</script>

<template>
  <div class="app-shell">
    <header data-test="topbar" class="topbar" :class="{ 'topbar--scrolled': isScrolled }">
      <div class="topbar__inner">
        <RouterLink class="brand" to="/" aria-label="返回概览">
          <span class="brand__mark" aria-hidden="true">D</span>
          <span class="brand__text">
            <strong>{{ runtimeConfig.appTitle }}</strong>
            <small>Workstation Control Plane</small>
          </span>
        </RouterLink>

        <el-menu
          class="topbar__nav"
          mode="horizontal"
          :default-active="activePath"
          :ellipsis="false"
          router
        >
          <el-menu-item index="/">概览</el-menu-item>
          <el-menu-item index="/#services">服务</el-menu-item>
          <el-menu-item index="/#identity">用户与权限</el-menu-item>
          <el-menu-item index="/#containers">容器</el-menu-item>
        </el-menu>

        <div class="topbar__actions">
          <span class="connection-state">
            <span class="connection-state__dot" />
            内部网络
          </span>
          <el-button
            class="theme-toggle"
            circle
            :aria-label="uiStore.isDark ? '切换到浅色主题' : '切换到深色主题'"
            @click="uiStore.toggleTheme"
          >
            <el-icon class="theme-toggle__icon">
              <component :is="uiStore.isDark ? Sunny : Moon" />
            </el-icon>
          </el-button>
          <el-button class="login-button" type="primary" round @click="login">
            <el-icon><UserFilled /></el-icon>
            登录
          </el-button>
        </div>
      </div>
      <span class="topbar__accent" aria-hidden="true" />
    </header>

    <main class="page-content">
      <RouterView />
    </main>
  </div>
</template>
