<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  ArrowDown,
  Box,
  Connection,
  DataAnalysis,
  Grid,
  Lock,
  Operation,
  Setting,
  Tickets,
  UserFilled,
} from '@element-plus/icons-vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import AppTopbar from '@/components/AppTopbar.vue'
import { authSession } from '@/services/authSession'

const route = useRoute()
const router = useRouter()
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
</script>

<template>
  <div class="app-shell">
    <AppTopbar show-navigation-trigger @toggle-navigation="toggleMobileNavigation" />

    <div class="shell-body">
      <aside class="sidebar" :class="{ 'sidebar--mobile-open': isMobileNavigationOpen }">
        <nav class="sidebar__navigation" aria-label="主导航">
          <template v-for="group in navigationGroups" :key="group.label">
            <div class="navigation-group">
              <span class="navigation-group__label">{{ group.label }}</span>
              <el-menu
                class="side-menu"
                :default-active="activeNavigation"
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
          <button class="sidebar-user" type="button" aria-label="打开用户菜单">
            <span class="sidebar-user__avatar">
              <el-icon><UserFilled /></el-icon>
            </span>
            <span class="sidebar-user__copy">
              <strong>{{ currentUserName }}</strong>
              <small>Authentik 账户</small>
            </span>
            <el-icon class="sidebar-user__arrow"><ArrowDown /></el-icon>
          </button>
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
