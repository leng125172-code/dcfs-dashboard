import { createRouter, createWebHistory } from 'vue-router'
import AppShell from '@/layouts/AppShell.vue'
import { authSession } from '@/services/authSession'
import LoginView from '@/views/LoginView.vue'
import OverviewView from '@/views/OverviewView.vue'

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
      ],
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
})

export default router
