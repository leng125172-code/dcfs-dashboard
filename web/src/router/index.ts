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
        { path: 'containers', name: 'containers', component: ResourceListView, meta: { administratorOnly: true, title: '容器', endpoint: 'containers' } },
        { path: 'docker/images', name: 'images', component: ResourceListView, meta: { administratorOnly: true, title: '镜像', endpoint: 'images' } },
        { path: 'docker/networks', name: 'networks', component: ResourceListView, meta: { administratorOnly: true, title: '网络', endpoint: 'networks' } },
        { path: 'docker/volumes', name: 'volumes', component: ResourceListView, meta: { administratorOnly: true, title: '数据卷', endpoint: 'volumes' } },
        { path: 'databases', name: 'databases', component: ResourceListView, meta: { administratorOnly: true, title: '数据库平台', endpoint: 'databases' } },
        { path: 'applications', name: 'applications', component: ResourceListView, meta: { administratorOnly: true, title: '应用', endpoint: 'applications' } },
        { path: 'systemd', name: 'systemd', component: ResourceListView, meta: { administratorOnly: true, title: '系统服务', endpoint: 'systemd' } },
        { path: 'identity/users', name: 'users', component: ResourceListView, meta: { administratorOnly: true, title: 'Authentik 用户', endpoint: 'users' } },
        { path: 'identity/groups', name: 'groups', component: ResourceListView, meta: { administratorOnly: true, title: 'Authentik 用户组', endpoint: 'groups' } },
        { path: 'identity/sso', name: 'sso', component: ResourceListView, meta: { administratorOnly: true, title: 'SSO 应用', endpoint: 'sso' } },
        { path: 'jobs', name: 'jobs', component: JobsView, meta: { administratorOnly: true, title: '任务中心' } },
        { path: 'schedules', name: 'schedules', component: RecordsView, meta: { administratorOnly: true, title: '计划任务', endpoint: 'schedules' } },
        { path: 'backups', name: 'backups', component: RecordsView, meta: { administratorOnly: true, title: '备份策略', endpoint: 'backups/policies' } },
        { path: 'alerts', name: 'alerts', component: RecordsView, meta: { administratorOnly: true, title: '告警', endpoint: 'alerts?includeRecovered=true' } },
        { path: 'audit', name: 'audit', component: RecordsView, meta: { administratorOnly: true, title: '审计日志', endpoint: 'audit?take=200' } },
        { path: 'settings', name: 'settings', component: RecordsView, meta: { administratorOnly: true, title: '平台设置', endpoint: 'settings' } },
      ],
    },
    { path: '/forbidden', name: 'forbidden', component: StateView, props: { code: '403', title: '没有管理权限', description: '当前账号可以使用首页和个人门户，但不能管理工作站。' } },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: StateView, props: { code: '404', title: '页面不存在', description: '这个地址没有对应的 Whale Deck 页面。' } },
  ],
})

router.beforeEach((to) => {
  if (authSession.initialized.value && to.meta.requiresAuth && !authSession.authenticated.value) {
    return { name: 'login', query: { returnUrl: to.fullPath } }
  }

  if (authSession.initialized.value && to.name === 'login' && authSession.authenticated.value) {
    return { name: 'overview' }
  }

  if (authSession.initialized.value && to.meta.administratorOnly && !authSession.user.value?.isAdministrator) {
    return { name: 'forbidden' }
  }
})

export default router
