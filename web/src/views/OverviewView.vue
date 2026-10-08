<script setup lang="ts">
import { Connection, DataAnalysis, Lock, Monitor } from '@element-plus/icons-vue'

const services = [
  {
    name: 'Dashboard API',
    description: 'ASP.NET Core 10',
    state: '骨架已就绪',
    tone: 'success',
    icon: Monitor,
  },
  {
    name: 'Authentik',
    description: '登录与用户身份资料',
    state: '等待接入',
    tone: 'warning',
    icon: Lock,
  },
  {
    name: 'PostgreSQL',
    description: '平台事务数据',
    state: '等待建库',
    tone: 'info',
    icon: DataAnalysis,
  },
  {
    name: 'Valkey',
    description: '缓存与短期状态',
    state: '等待接入',
    tone: 'info',
    icon: Connection,
  },
] as const
</script>

<template>
  <section class="hero">
    <div class="hero__glow hero__glow--primary" />
    <div class="hero__glow hero__glow--secondary" />
    <div class="hero__content">
      <el-tag type="primary" effect="light" round>DCFS CONTROL PLANE</el-tag>
      <h1>一处管理工作站上的服务、身份与运行状态</h1>
      <p>
        统一入口已经建立。后续将从这里接入 Authentik、GitLab、数据库状态以及受限的容器管理代理。
      </p>
      <div class="hero__actions">
        <el-button type="primary" size="large" round>查看部署状态</el-button>
        <el-button size="large" round plain>阅读架构说明</el-button>
      </div>
    </div>
  </section>

  <section id="services" class="content-section">
    <div class="section-heading">
      <div>
        <span class="section-heading__eyebrow">PLATFORM STATUS</span>
        <h2>核心服务</h2>
      </div>
      <el-button text>刷新状态</el-button>
    </div>

    <div class="service-grid">
      <article v-for="service in services" :key="service.name" class="service-card">
        <div class="service-card__icon">
          <el-icon><component :is="service.icon" /></el-icon>
        </div>
        <div class="service-card__body">
          <h3>{{ service.name }}</h3>
          <p>{{ service.description }}</p>
        </div>
        <el-tag :type="service.tone" effect="light" round>{{ service.state }}</el-tag>
      </article>
    </div>
  </section>

  <section id="identity" class="content-section info-panel">
    <div>
      <span class="section-heading__eyebrow">IDENTITY</span>
      <h2>登录与用户资料全部由 Authentik 管理</h2>
      <p>
        Dashboard 不保存本地密码或复制用户档案。后端通过 OIDC/BFF 完成登录，并以 Authentik 用户 ID
        和组声明执行平台授权。
      </p>
    </div>
    <el-tag type="warning" size="large" effect="plain">待初始化 Provider</el-tag>
  </section>

  <section id="containers" class="content-section info-panel">
    <div>
      <span class="section-heading__eyebrow">CONTAINERS</span>
      <h2>容器控制保持独立安全边界</h2>
      <p>后续通过受限 Agent 执行白名单操作，Dashboard API 不直接挂载 Docker Socket。</p>
    </div>
    <el-tag type="info" size="large" effect="plain">规划中</el-tag>
  </section>
</template>
