import { describe, expect, it } from 'vitest'

import { mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import BootScreen from '@/components/BootScreen.vue'
import AppShell from '@/layouts/AppShell.vue'
import LoginView from '@/views/LoginView.vue'
import router from '@/router'

const globalPlugins = () => [createPinia(), router, ElementPlus]

describe('WhaleDeck entry flow', () => {
  it('renders the platform shell after authentication', async () => {
    const wrapper = mount(AppShell, {
      global: {
        plugins: globalPlugins(),
      },
    })

    expect(wrapper.text()).toContain('WhaleDeck 协同平台')
    expect(wrapper.get('[data-test="topbar"]').classes()).toContain('topbar')
    expect(wrapper.find('.topbar__context').exists()).toBe(false)
    expect(wrapper.find('.theme-switch .el-switch').exists()).toBe(true)
  })

  it('renders the startup loading state', () => {
    const wrapper = mount(BootScreen)

    expect(wrapper.text()).toContain('正在连接身份与平台服务')
    expect(wrapper.findAll('.boot-screen__dots i')).toHaveLength(3)
  })

  it('delegates login to Authentik without collecting a password', async () => {
    await router.push('/login')
    await router.isReady()

    const wrapper = mount(LoginView, {
      global: {
        plugins: globalPlugins(),
      },
    })

    expect(wrapper.text()).toContain('使用用户名和密码登录')
    expect(wrapper.find('input[type="password"]').exists()).toBe(false)
    expect(wrapper.get('.authentik-login')).toBeTruthy()
    expect(wrapper.find('.theme-switch .el-switch').exists()).toBe(true)
  })
})
