<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  ArrowDown,
  Box,
  DataAnalysis,
  Grid,
  Link,
  Monitor,
  Odometer,
  Operation,
  Setting,
  BellFilled,
  Calendar,
  Document,
  UserFilled,
} from '@element-plus/icons-vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import AppTopbar from '@/components/AppTopbar.vue'
import { authSession } from '@/services/authSession'

const route = useRoute()
const router = useRouter()
const isMobileNavigationOpen = ref(false)

const activeNavigation = computed(() => route.path)
const currentUserName = computed(() => authSession.user.value?.name || '已登录')
const isAdministrator = computed(() => authSession.user.value?.isAdministrator === true)

const navigationGroups = [
  {
    label: '工作站',
    items: [
      { index: '/', label: '工作站概览', icon: Grid, administratorOnly: false },
      { index: '/portal', label: '我的门户', icon: Link, administratorOnly: false },
    ],
  },
  {
    label: '运行与监控',
    items: [
      { index: '/containers', label: '容器', icon: DataAnalysis, administratorOnly: true },
      { index: '/databases', label: '数据库', icon: Odometer, administratorOnly: true },
      { index: '/systemd', label: '系统服务', icon: Monitor, administratorOnly: true },
      { index: '/jobs', label: '任务中心', icon: Setting, administratorOnly: true },
      { index: '/schedules', label: '计划任务', icon: Calendar, administratorOnly: true },
    ],
  },
  {
    label: '应用',
    items: [
      { index: '/applications', label: '应用管理', icon: Box, administratorOnly: true },
      { index: '/backups', label: '备份策略', icon: Document, administratorOnly: true },
    ],
  },
  {
    label: '治理',
    items: [
      { index: '/alerts', label: '告警', icon: BellFilled, administratorOnly: true },
      { index: '/audit', label: '审计日志', icon: Document, administratorOnly: true },
      { index: '/settings', label: '平台设置', icon: Setting, administratorOnly: true },
    ],
  },
] as const

const visibleNavigationGroups = computed(() => navigationGroups
  .map((group) => ({ ...group, items: group.items.filter((item) => !item.administratorOnly || isAdministrator.value) }))
  .filter((group) => group.items.length > 0))

function toggleMobileNavigation() {
  isMobileNavigationOpen.value = !isMobileNavigationOpen.value
}

async function selectNavigation(index: string) {
  isMobileNavigationOpen.value = false
  await router.push(index)
}

async function userCommand(command: string) {
  if (command === 'portal') await router.push('/portal')
  if (command === 'logout') await authSession.logout()
}
</script>

<template>
  <div class="app-shell">
    <AppTopbar show-navigation-trigger @toggle-navigation="toggleMobileNavigation" />

    <div class="shell-body">
      <aside class="sidebar" :class="{ 'sidebar--mobile-open': isMobileNavigationOpen }">
        <nav class="sidebar__navigation" aria-label="主导航">
          <template v-for="group in visibleNavigationGroups" :key="group.label">
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
          <el-dropdown trigger="click" placement="top-start" @command="userCommand">
          <button class="sidebar-user" type="button" aria-label="打开用户菜单">
            <span class="sidebar-user__avatar">
              <el-icon><UserFilled /></el-icon>
            </span>
            <span class="sidebar-user__copy">
              <strong>{{ currentUserName }}</strong>
              <small>{{ isAdministrator ? '管理员' : '普通用户' }} · Authentik</small>
            </span>
            <el-icon class="sidebar-user__arrow"><ArrowDown /></el-icon>
          </button>
          <template #dropdown><el-dropdown-menu><el-dropdown-item command="portal">我的门户</el-dropdown-item><el-dropdown-item divided command="logout">退出登录</el-dropdown-item></el-dropdown-menu></template>
          </el-dropdown>
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
