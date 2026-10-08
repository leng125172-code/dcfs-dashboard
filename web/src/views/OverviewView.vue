<script setup lang="ts">
import {
  ArrowRight,
  Box,
  Connection,
  DataAnalysis,
  Lock,
  Monitor,
  Refresh,
  User,
} from '@element-plus/icons-vue'

const metrics = [
  { label: '平台服务', value: '4', suffix: '项', detail: '1 项基础骨架就绪', tone: 'primary' },
  { label: '身份来源', value: '1', suffix: '个', detail: 'Authentik 统一管理', tone: 'success' },
  { label: '数据连接', value: '2', suffix: '个', detail: 'PostgreSQL + Valkey', tone: 'warning' },
  { label: '外部暴露', value: '0', suffix: '个', detail: '仅网关监听主机端口', tone: 'info' },
] as const

const services = [
  {
    name: 'WhaleDeck API',
    description: 'ASP.NET Core 10 · BFF 与管理 API',
    state: '骨架就绪',
    tone: 'success',
    icon: Monitor,
  },
  {
    name: 'Authentik',
    description: '登录、用户资料、用户组与 MFA',
    state: '等待 Provider',
    tone: 'warning',
    icon: Lock,
  },
  {
    name: 'PostgreSQL',
    description: '平台事务数据与审计记录',
    state: '等待建库',
    tone: 'info',
    icon: DataAnalysis,
  },
  {
    name: 'Valkey',
    description: '缓存、短期状态与后台任务协调',
    state: '等待接入',
    tone: 'info',
    icon: Connection,
  },
] as const

const quickActions = [
  { label: '配置 Authentik Provider', description: '建立 WhaleDeck OIDC 客户端', icon: Lock },
  { label: '初始化平台数据库', description: '执行迁移并创建最小权限账号', icon: DataAnalysis },
  { label: '检查容器网络', description: '验证内部网络与网关边界', icon: Box },
] as const
</script>

<template>
  <section id="overview" class="page-heading">
    <div>
      <div class="page-heading__meta">
        <el-tag size="small" type="primary" effect="light" round>WORKSTATION</el-tag>
        <span>192.168.22.19</span>
      </div>
      <h1>工作站概览</h1>
      <p>集中查看服务、身份、数据与容器管理面的配置进度。</p>
    </div>
    <div class="page-heading__actions">
      <el-button>
        <el-icon><Refresh /></el-icon>
        刷新状态
      </el-button>
      <el-button type="primary">
        查看部署清单
        <el-icon><ArrowRight /></el-icon>
      </el-button>
    </div>
  </section>

  <section class="metric-grid" aria-label="平台概要">
    <article
      v-for="metric in metrics"
      :key="metric.label"
      class="metric-card"
      :class="`metric-card--${metric.tone}`"
    >
      <div class="metric-card__heading">
        <span>{{ metric.label }}</span>
        <span class="metric-card__signal" />
      </div>
      <strong
        >{{ metric.value }}<small>{{ metric.suffix }}</small></strong
      >
      <p>{{ metric.detail }}</p>
    </article>
  </section>

  <section id="services" class="dashboard-grid dashboard-grid--services content-anchor">
    <article class="panel service-panel">
      <header class="panel__header">
        <div>
          <span class="panel__eyebrow">SERVICE STATUS</span>
          <h2>核心服务</h2>
        </div>
        <el-button text type="primary"
          >全部服务 <el-icon><ArrowRight /></el-icon
        ></el-button>
      </header>

      <div class="service-list">
        <div v-for="service in services" :key="service.name" class="service-row">
          <span class="service-row__icon">
            <el-icon><component :is="service.icon" /></el-icon>
          </span>
          <span class="service-row__copy">
            <strong>{{ service.name }}</strong>
            <small>{{ service.description }}</small>
          </span>
          <el-tag :type="service.tone" effect="light" round>{{ service.state }}</el-tag>
          <el-button class="service-row__action" text circle aria-label="查看服务详情">
            <el-icon><ArrowRight /></el-icon>
          </el-button>
        </div>
      </div>
    </article>

    <article class="panel quick-panel">
      <header class="panel__header">
        <div>
          <span class="panel__eyebrow">NEXT STEPS</span>
          <h2>下一步</h2>
        </div>
        <span class="step-count">3 项</span>
      </header>

      <button
        v-for="(action, index) in quickActions"
        :key="action.label"
        class="quick-action"
        type="button"
      >
        <span class="quick-action__index">0{{ index + 1 }}</span>
        <el-icon><component :is="action.icon" /></el-icon>
        <span>
          <strong>{{ action.label }}</strong>
          <small>{{ action.description }}</small>
        </span>
        <el-icon class="quick-action__arrow"><ArrowRight /></el-icon>
      </button>
    </article>
  </section>

  <section class="dashboard-grid content-anchor">
    <article id="identity" class="panel feature-panel">
      <div class="feature-panel__icon feature-panel__icon--identity">
        <el-icon><User /></el-icon>
      </div>
      <div class="feature-panel__content">
        <div class="feature-panel__title">
          <span class="panel__eyebrow">IDENTITY</span>
          <el-tag type="warning" effect="plain" round>待接入</el-tag>
        </div>
        <h2>用户与权限</h2>
        <p>
          WhaleDeck 不保存密码或复制用户档案。登录、姓名、邮箱、用户组及 MFA 全部以 Authentik 为准。
        </p>
        <el-button text type="primary"
          >查看认证边界 <el-icon><ArrowRight /></el-icon
        ></el-button>
      </div>
    </article>

    <article id="containers" class="panel feature-panel">
      <div class="feature-panel__icon feature-panel__icon--container">
        <el-icon><Box /></el-icon>
      </div>
      <div class="feature-panel__content">
        <div class="feature-panel__title">
          <span class="panel__eyebrow">CONTAINERS</span>
          <el-tag type="info" effect="plain" round>规划中</el-tag>
        </div>
        <h2>容器管理</h2>
        <p>
          通过受限 Agent 执行白名单操作，管理 API 不直接挂载 Docker Socket，保留明确的权限边界。
        </p>
        <el-button text type="primary"
          >查看容器结构 <el-icon><ArrowRight /></el-icon
        ></el-button>
      </div>
    </article>
  </section>

  <section id="database" class="panel data-panel content-anchor">
    <header class="panel__header">
      <div>
        <span class="panel__eyebrow">DATA PLATFORM</span>
        <h2>数据平台连接</h2>
      </div>
      <el-tag type="info" effect="plain" round>内部网络</el-tag>
    </header>
    <div class="data-platforms">
      <div>
        <span class="data-platforms__badge"
          ><el-icon><DataAnalysis /></el-icon
        ></span>
        <span><strong>PostgreSQL</strong><small>平台主数据库</small></span>
        <em>等待建库</em>
      </div>
      <div>
        <span class="data-platforms__badge data-platforms__badge--cache">
          <el-icon><Connection /></el-icon>
        </span>
        <span><strong>Valkey</strong><small>缓存与短期状态</small></span>
        <em>等待 ACL</em>
      </div>
    </div>
  </section>

  <section class="footer-panels">
    <article id="audit" class="footer-panel content-anchor">
      <span class="panel__eyebrow">AUDIT</span>
      <h3>审计日志</h3>
      <p>关键管理操作将进入不可变审计记录。</p>
    </article>
    <article id="settings" class="footer-panel content-anchor">
      <span class="panel__eyebrow">SETTINGS</span>
      <h3>平台设置</h3>
      <p>运行参数统一由环境变量与平台配置管理。</p>
    </article>
  </section>
</template>
