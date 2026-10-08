import { describe, it, expect } from 'vitest'

import { mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import App from '../App.vue'
import router from '../router'

describe('App', () => {
  it('renders the platform shell', async () => {
    await router.push('/')
    await router.isReady()

    const wrapper = mount(App, {
      global: {
        plugins: [createPinia(), router, ElementPlus],
      },
    })

    expect(wrapper.text()).toContain('DCFS 协同平台')
    expect(wrapper.get('[data-test="topbar"]').classes()).toContain('topbar')
  })
})
