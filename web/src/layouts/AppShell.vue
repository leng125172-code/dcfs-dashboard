<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import {
  ArrowDown,
  Bell,
  Box,
  Connection,
  DataAnalysis,
  Expand,
  Fold,
  Grid,
  Lock,
  Menu as MenuIcon,
  Operation,
  Search,
  Setting,
  Tickets,
  UserFilled,
} from '@element-plus/icons-vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import ThemeSwitch from '@/components/ThemeSwitch.vue'
import { runtimeConfig } from '@/config/runtime'
import { authSession } from '@/services/authSession'

const route = useRoute()
const router = useRouter()
const whaleMarkUrl = '/images/whaledeck-mark.png'
const isScrolled = ref(false)
const isSidebarCollapsed = ref(false)
const isMobileNavigationOpen = ref(false)

const activeNavigation = computed(() => route.hash.slice(1) || 'overview')
const currentUserName = computed(() => authSession.user.value?.name || '已登录')

const navigationGroups = [
  {
    label: '工作区',
    items: [{ index: 'overview', label: '工作站概览', icon: Grid }],
  },
  {
    label: '平台管理',
    items: [
      { index: 'services', label: '服务状态', icon: Connection },
      { index: 'identity', label: '用户与权限', icon: Lock },
      { index: 'containers', label: '容器管理', icon: Box },
      { index: 'database', label: '数据平台', icon: DataAnalysis },
    ],
  },
  {
    label: '系统',
    items: [
      { index: 'audit', label: '审计日志', icon: Tickets },
      { index: 'settings', label: '平台设置', icon: Setting },
    ],
  },
] as const

function updateScrollState() {
  isScrolled.value = window.scrollY > 8
}

function toggleSidebar() {
  isSidebarCollapsed.value = !isSidebarCollapsed.value
}

function toggleMobileNavigation() {
  isMobileNavigationOpen.value = !isMobileNavigationOpen.value
}

async function selectNavigation(index: string) {
  isMobileNavigationOpen.value = false
  await router.push(
    index === 'overview' ? { path: '/', hash: '' } : { path: '/', hash: `#${index}` },
  )
  requestAnimationFrame(() => {
    document.getElementById(index)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  })
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
  <div class="app-shell" :class="{ 'app-shell--collapsed': isSidebarCollapsed }">
    <header
      data-test="topbar"
      class="topbar"
      :class="{ 'topbar--scrolled': isScrolled }"
      @pointermove="updateTopbarSpotlight"
      @pointerleave="hideTopbarSpotlight"
    >
      <RouterLink class="brand" to="/" aria-label="返回工作站概览">
        <span class="brand__mark brand__mark--image" aria-hidden="true">
          <img :src="whaleMarkUrl" alt="" />
        </span>
        <span class="brand__text">
          <strong>{{ runtimeConfig.appTitle }}</strong>
          <small>Control Plane</small>
        </span>
      </RouterLink>

      <div class="topbar__workspace">
        <el-button
          class="navigation-trigger navigation-trigger--mobile"
          text
          circle
          aria-label="打开导航"
          @click="toggleMobileNavigation"
        >
          <el-icon><MenuIcon /></el-icon>
        </el-button>
        <el-button
          class="navigation-trigger navigation-trigger--desktop"
          text
          circle
          :aria-label="isSidebarCollapsed ? '展开侧边导航' : '收起侧边导航'"
          @click="toggleSidebar"
        >
          <el-icon><component :is="isSidebarCollapsed ? Expand : Fold" /></el-icon>
        </el-button>

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
              <el-badge is-dot
                ><el-icon><Bell /></el-icon
              ></el-badge>
            </el-button>
          </el-tooltip>
          <ThemeSwitch />
          <el-button class="login-button user-button" type="primary" plain round>
            <el-icon><UserFilled /></el-icon>
            <span>{{ currentUserName }}</span>
            <el-icon class="login-button__arrow"><ArrowDown /></el-icon>
          </el-button>
        </div>
      </div>
      <span class="topbar__accent" aria-hidden="true" />
    </header>

    <div class="shell-body">
      <aside class="sidebar" :class="{ 'sidebar--mobile-open': isMobileNavigationOpen }">
        <nav class="sidebar__navigation" aria-label="主导航">
          <template v-for="group in navigationGroups" :key="group.label">
            <div class="navigation-group">
              <span class="navigation-group__label">{{ group.label }}</span>
              <el-menu
                class="side-menu"
                :default-active="activeNavigation"
                :collapse="isSidebarCollapsed"
                :collapse-transition="false"
                @select="selectNavigation"
              >
                <el-menu-item v-for="item in group.items" :key="item.index" :index="item.index">
                  <el-icon><component :is="item.icon" /></el-icon>
                  <template #title>{{ item.label }}</template>
                </el-menu-item>
              </el-menu>
            </div>
          </template>
        </nav>

        <div class="sidebar__footer">
          <div class="sidebar-status">
            <span class="sidebar-status__icon"
              ><el-icon><Operation /></el-icon
            ></span>
            <span class="sidebar-status__copy">
              <strong>平台通道</strong>
              <small>等待服务接入</small>
            </span>
          </div>
        </div>
      </aside>

      <button
        v-if="isMobileNavigationOpen"
        class="navigation-backdrop"
        type="button"
        aria-label="关闭导航"
        @click="isMobileNavigationOpen = false"
      />

      <main class="main-stage">
        <div class="page-content">
          <RouterView />
        </div>
      </main>
    </div>
  </div>
</template>
