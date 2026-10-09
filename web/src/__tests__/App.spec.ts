import { describe, expect, it } from 'vitest'

import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import BootScreen from '@/components/BootScreen.vue'
import AppShell from '@/layouts/AppShell.vue'
import LoginView from '@/views/LoginView.vue'
import OverviewView from '@/views/OverviewView.vue'
import router from '@/router'

const globalPlugins = () => [createPinia(), router, ElementPlus]

describe('WhaleDeck entry flow', () => {
  it('renders the platform shell after authentication', async () => {
    const wrapper = mount(AppShell, {
      global: {
        plugins: globalPlugins(),
      },
    })

    expect(wrapper.text()).toContain('Whale Deck')
    expect(wrapper.get('[data-test="topbar"]').classes()).toContain('topbar')
    expect(wrapper.find('.topbar__context').exists()).toBe(false)
    expect(wrapper.find('.theme-switch .el-switch').exists()).toBe(true)
    expect(wrapper.find('.navigation-trigger--desktop').exists()).toBe(false)
    expect(wrapper.find('.sidebar-user').exists()).toBe(true)
  })

  it('renders the startup loading state', () => {
    const wrapper = mount(BootScreen)

    expect(wrapper.text()).toContain('正在连接身份与平台服务')
    expect(wrapper.findAll('.boot-screen__dots i')).toHaveLength(3)
  })

  it('renders workstation telemetry and recommended applications', async () => {
    const wrapper = mount(OverviewView, {
      global: {
        plugins: globalPlugins(),
      },
    })

    await flushPromises()

    expect(wrapper.text()).toContain('主机资源正常')
    expect(wrapper.text()).toContain('流量监控')
    expect(wrapper.text()).toContain('磁盘 IO')
    expect(wrapper.find('.telemetry-chart').exists()).toBe(true)
    expect(wrapper.findAll('.system-item__copy')).toHaveLength(2)
    expect(wrapper.text()).not.toContain('演示值')
    expect(wrapper.findAll('.application-card')).toHaveLength(6)
    const installedApplication = wrapper.find('.application-card--installed')
    expect(installedApplication.text()).toContain('运行中')
    expect(installedApplication.text()).toContain('关闭')
    expect(installedApplication.text()).toContain('重启')
    expect(installedApplication.text()).toContain('管理')
    expect(installedApplication.text()).not.toContain('安装')
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
    expect(wrapper.find('[data-test="topbar"]').exists()).toBe(true)
    expect(wrapper.find('.command-search').exists()).toBe(false)
    expect(wrapper.find('.topbar__actions').exists()).toBe(false)
  })
})
