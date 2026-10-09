<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  ArrowDown,
  Box,
  DataAnalysis,
  Grid,
  Monitor,
  Odometer,
  Operation,
  Setting,
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
    label: '工作站',
    items: [{ index: 'overview', label: '工作站概览', icon: Grid }],
  },
  {
    label: '运行与监控',
    items: [
      { index: 'resources', label: '资源概览', icon: DataAnalysis },
      { index: 'status', label: '运行状态', icon: Odometer },
      { index: 'monitoring', label: '实时监控', icon: Monitor },
      { index: 'system', label: '系统信息', icon: Setting },
    ],
  },
  {
    label: '应用',
    items: [{ index: 'containers', label: '容器推荐', icon: Box }],
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
              <strong>预览数据</strong>
              <small>等待工作站 Agent</small>
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
