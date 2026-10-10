import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import BootScreen from '@/components/BootScreen.vue'
import AppShell from '@/layouts/AppShell.vue'
import LoginView from '@/views/LoginView.vue'
import OverviewView from '@/views/OverviewView.vue'
import router from '@/router'
import { fetchWorkstationOverview } from '@/services/workstationOverview'

const globalPlugins = () => [createPinia(), router, ElementPlus]

const jsonResponse = (value: unknown) =>
  new Response(JSON.stringify(value), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })

beforeEach(() => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/metrics?')) {
        const points = [
          { sampledAtUtc: '2026-10-10T10:00:00Z', value: 1024 * 1024, quality: 'Good' },
          { sampledAtUtc: '2026-10-10T10:00:06Z', value: 2 * 1024 * 1024, quality: 'Good' },
        ]
        return jsonResponse([
          { kind: 'network.receive', deviceId: 'eno1', unit: 'bytesPerSecond', points },
          { kind: 'network.send', deviceId: 'eno1', unit: 'bytesPerSecond', points },
          { kind: 'network.receive', deviceId: 'vethdeadbeef', unit: 'bytesPerSecond', points },
          { kind: 'network.send', deviceId: 'vethdeadbeef', unit: 'bytesPerSecond', points },
          { kind: 'disk.read', deviceId: 'nvme0n1', unit: 'bytesPerSecond', points },
          { kind: 'disk.write', deviceId: 'nvme0n1', unit: 'bytesPerSecond', points },
        ])
      }
      if (url.endsWith('/overview')) {
        return jsonResponse({
          scope: 'Administrator',
          platformStatus: 'Ready',
          sampledAtUtc: '2026-10-10T10:00:06Z',
          portals: [
            {
              id: '63d533e4-a53a-4072-a42b-c04ee68dd9d1',
              scope: 'Personal',
              name: '开发门户',
              description: '常用开发服务',
              url: 'http://precision-7920-tower.local:8080',
              iconKind: 'BuiltIn',
              iconValue: 'Link',
              color: 'primary',
              sortOrder: 0,
              isEnabled: true,
              version: 1,
            },
          ],
          agent: {
            available: true,
            dockerAvailable: true,
            systemdAvailable: true,
            status: 'Ready',
            observedAtUtc: '2026-10-10T10:00:06Z',
          },
          host: {
            hostName: 'Precision-7920-Tower',
            distribution: 'Ubuntu 24.04 LTS',
            kernelVersion: 'Linux 6.17',
            architecture: 'X64',
            bootTimeUtc: '2026-10-09T10:00:00Z',
            uptime: '1.00:00:06',
            logicalProcessorCount: 48,
            totalMemoryBytes: 128 * 1024 ** 3,
            addresses: [
              { interfaceName: 'eno1', address: '192.168.22.19' },
              { interfaceName: 'usb0', address: '192.168.100.13' },
            ],
          },
          resources: Array.from({ length: 11 }, (_, index) => ({
            id: `container-${index}`,
            name: index < 8 ? `database-platform-${index}` : `whaledeck-${index}`,
            type: 'Container',
            state: index === 10 ? 'exited' : 'running',
            version: 'test:latest',
            isProtected: index < 8,
            attributes: {},
          })),
          metrics: [
            { kind: 'cpu.utilization', deviceId: 'host', value: 18.6, unit: 'percent' },
            { kind: 'memory.utilization', deviceId: 'host', value: 42.8, unit: 'percent' },
            { kind: 'disk.utilization', deviceId: '/', value: 6.1, unit: 'percent' },
            { kind: 'network.receive.total', deviceId: 'eno1', value: 1024 ** 4, unit: 'bytes' },
            { kind: 'network.send.total', deviceId: 'eno1', value: 512 * 1024 ** 3, unit: 'bytes' },
          ].map((item) => ({ ...item, quality: 'Good', sampledAtUtc: '2026-10-10T10:00:06Z' })),
          applications: Array.from({ length: 6 }, (_, index) => ({
            id: `application-${index}`,
            name: `应用 ${index + 1}`,
            description: '来自真实目录快照',
            image: `example/app-${index}:1.0`,
            version: '1.0',
            iconUrl: null,
            installed: index === 0,
            state: index === 0 ? 'running' : null,
          })),
          catalogIsStale: false,
        })
      }
      return new Response(null, { status: 404 })
    }),
  )
})

afterEach(() => vi.unstubAllGlobals())

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

  it('keeps Docker virtual interfaces out of workstation telemetry', async () => {
    const snapshot = await fetchWorkstationOverview()
    expect(snapshot.network.filters.map((item) => item.value)).toEqual(['all', 'eno1'])
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
