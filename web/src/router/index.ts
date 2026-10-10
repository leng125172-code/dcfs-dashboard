import { createRouter, createWebHistory } from 'vue-router'
import AppShell from '@/layouts/AppShell.vue'
import { authSession } from '@/services/authSession'
import LoginView from '@/views/LoginView.vue'
import OverviewView from '@/views/OverviewView.vue'
import PortalView from '@/views/PortalView.vue'
import ResourceListView from '@/views/ResourceListView.vue'
import JobsView from '@/views/JobsView.vue'
import StateView from '@/views/StateView.vue'
import RecordsView from '@/views/RecordsView.vue'
import ContainersView from '@/views/ContainersView.vue'
import ApplicationsView from '@/views/ApplicationsView.vue'
import BackupsView from '@/views/BackupsView.vue'
import ConfigRepositoryView from '@/views/ConfigRepositoryView.vue'
import DatabasesView from '@/views/DatabasesView.vue'
import DockerSettingsView from '@/views/DockerSettingsView.vue'
import IdentityView from '@/views/IdentityView.vue'
import PlatformMaintenanceView from '@/views/PlatformMaintenanceView.vue'
import SchedulesView from '@/views/SchedulesView.vue'
import AlertsView from '@/views/AlertsView.vue'
import SettingsView from '@/views/SettingsView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: LoginView,
    },
    {
      path: '/',
      component: AppShell,
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          name: 'overview',
          component: OverviewView,
        },
        { path: 'portal', name: 'portal', component: PortalView, meta: { title: '我的门户' } },
        {
          path: 'containers',
          name: 'containers',
          component: ContainersView,
          meta: { administratorOnly: true, title: '容器' },
        },
        {
          path: 'docker/images',
          name: 'images',
          component: ResourceListView,
          meta: { administratorOnly: true, title: '镜像', endpoint: 'images' },
        },
        {
          path: 'docker/networks',
          name: 'networks',
          component: ResourceListView,
          meta: { administratorOnly: true, title: '网络', endpoint: 'networks' },
        },
        {
          path: 'docker/volumes',
          name: 'volumes',
          component: ResourceListView,
          meta: { administratorOnly: true, title: '数据卷', endpoint: 'volumes' },
        },
        {
          path: 'docker/settings',
          name: 'docker-settings',
          component: DockerSettingsView,
          meta: { administratorOnly: true, title: 'Docker 设置' },
        },
        {
          path: 'databases',
          name: 'databases',
          component: DatabasesView,
          meta: { administratorOnly: true, title: '数据库平台' },
        },
        {
          path: 'applications',
          name: 'applications',
          component: ApplicationsView,
          meta: { administratorOnly: true, title: '应用' },
        },
        {
          path: 'systemd',
          name: 'systemd',
          component: ResourceListView,
          meta: { administratorOnly: true, title: '系统服务', endpoint: 'systemd' },
        },
        {
          path: 'identity/users',
          name: 'users',
          component: IdentityView,
          meta: { administratorOnly: true, title: 'Authentik 用户', identityMode: 'users' },
        },
        {
          path: 'identity/groups',
          name: 'groups',
          component: IdentityView,
          meta: { administratorOnly: true, title: 'Authentik 用户组', identityMode: 'groups' },
        },
        {
          path: 'identity/sso',
          name: 'sso',
          component: IdentityView,
          meta: { administratorOnly: true, title: 'SSO 应用', identityMode: 'sso' },
        },
        {
          path: 'jobs',
          name: 'jobs',
          component: JobsView,
          meta: { administratorOnly: true, title: '任务中心' },
        },
        {
          path: 'schedules',
          name: 'schedules',
          component: SchedulesView,
          meta: { administratorOnly: true, title: '计划任务' },
        },
        {
          path: 'backups',
          name: 'backups',
          component: BackupsView,
          meta: { administratorOnly: true, title: '备份策略' },
        },
        {
          path: 'alerts',
          name: 'alerts',
          component: AlertsView,
          meta: { administratorOnly: true, title: '告警' },
        },
        {
          path: 'audit',
          name: 'audit',
          component: RecordsView,
          meta: { administratorOnly: true, title: '审计日志', endpoint: 'audit?take=200' },
        },
        {
          path: 'settings',
          name: 'settings',
          component: SettingsView,
          meta: { administratorOnly: true, title: '平台设置' },
        },
        {
          path: 'settings/config-repository',
          name: 'config-repository',
          component: ConfigRepositoryView,
          meta: { administratorOnly: true, title: '配置仓库' },
        },
        {
          path: 'platform/maintenance',
          name: 'platform-maintenance',
          component: PlatformMaintenanceView,
          meta: { administratorOnly: true, title: '平台维护' },
        },
      ],
    },
    {
      path: '/forbidden',
      name: 'forbidden',
      component: StateView,
      props: {
        code: '403',
        title: '没有管理权限',
        description: '当前账号可以使用首页和个人门户，但不能管理工作站。',
      },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: StateView,
      props: {
        code: '404',
        title: '页面不存在',
        description: '这个地址没有对应的 Whale Deck 页面。',
      },
    },
  ],
})

router.beforeEach((to) => {
  if (authSession.initialized.value && to.meta.requiresAuth && !authSession.authenticated.value) {
    return { name: 'login', query: { returnUrl: to.fullPath } }
  }

  if (authSession.initialized.value && to.name === 'login' && authSession.authenticated.value) {
    return { name: 'overview' }
  }

  if (
    authSession.initialized.value &&
    to.meta.administratorOnly &&
    !authSession.user.value?.isAdministrator
  ) {
    return { name: 'forbidden' }
  }
})

export default router
