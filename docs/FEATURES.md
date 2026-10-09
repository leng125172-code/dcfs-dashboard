# WhaleDeck 前后端功能基线

> 本文是可持续补充的功能清单，不是冻结版 PRD。它以当前仓库、`database-platform` 的实际部署、既有界面约定和用户已确认需求为基线，并参考 1Panel、Cockpit、Portainer、Webmin 等 Linux 面板的功能分层。

- 文档日期：2026-10-09
- 当前开发分支：`dev`
- 首期部署对象：Precision 7920 单工作站
- 前端：Vue 3、TypeScript、Element Plus
- 后端：ASP.NET Core 10、.NET Worker、EF Core、PostgreSQL、Valkey
- 身份源：Authentik

## 1. 状态标记

| 标记 | 含义 |
| --- | --- |
| `DONE` | 当前代码已具备基础能力 |
| `DEMO` | 前端已有演示效果，尚未连接真实数据或操作 |
| `MVP` | 首个可用版本必须完成 |
| `P1` | MVP 后优先补充 |
| `P2` | 后续增强能力 |
| `HOLD` | 已有方向，但暂不实施 |

## 2. 产品定位与边界

WhaleDeck 是 Precision 7920 工作站的内部协同与运维控制面，不以替代所有专业运维工具为目标。它负责把身份、工作站状态、容器、数据库、应用、用户同步和审计收拢到一个入口。

### 2.1 已确认边界

- 使用 IP 地址访问，不依赖域名。
- 当前保持 HTTP，不启用 HTTPS；未来如跨越受信任局域网再重新评估。
- 内网地址为 `192.168.22.19`，固定 IP、无外网，允许访问 WhaleDeck 和容器入口。
- 外网局域网地址为 `192.168.100.13`，用于本机下载及局域网访问，也允许登录 WhaleDeck。
- 只有统一网关发布宿主机端口；API、Worker、Agent、数据库、缓存和 Authentik 不直接发布端口。
- Authentik 是用户名、密码、MFA、用户资料、账户状态和用户组的唯一事实来源。
- WhaleDeck 不保存本地密码，不复制 Authentik 用户资料表。
- WhaleDeck API 与 Worker 容器不挂载 `/var/run/docker.sock`。
- 容器管理和宿主机管理通过单独的 `WhaleDeck.Agent` 完成。
- 数据库运行数据放在 NVMe，备份写入 HDD；HDD 备份不等同异地灾备。
- 容器和应用日志必须有界，禁止无上限文件日志及高频信息日志。
- 工作站每周日 20:00 安装普通系统更新，不自动重启。

### 2.2 暂不作为首期目标

- Kubernetes、Docker Swarm 和多节点集群编排。
- 公网暴露、域名管理和证书自动签发。
- 完整替代 Portainer、pgAdmin、SSMS、MongoDB Compass 等专业工具。
- 任意宿主机命令执行和无约束的 Web 终端。
- 自动重启宿主机或 Docker，除非用户明确配置维护窗口。

## 3. 总体架构

```text
浏览器
  │
  ▼
WhaleDeck Gateway
  ├── Vue 静态资源
  ├── /api/*      ──► WhaleDeck.Api
  └── /auth/*     ──► Authentik

WhaleDeck.Api
  ├── Authentik OIDC / BFF 会话
  ├── PostgreSQL：设置、权限映射、审计、任务历史
  ├── Valkey：缓存、短期状态、分布式锁、任务进度
  ├── WhaleDeck.Worker：后台同步、采集、计划任务
  └── /run/whaledeck/agent.sock ──► WhaleDeck.Agent

WhaleDeck.Agent（宿主机 systemd 服务）
  ├── Docker Engine API
  ├── systemd / journal
  ├── /proc / /sys
  ├── 网络、磁盘、进程与主机信息
  └── 经过白名单的宿主机操作
```

### 3.1 组件职责

| 组件 | 主要职责 | 禁止事项 |
| --- | --- | --- |
| Gateway | 单一访问入口、静态前端、反向代理、安全响应头 | 不保存业务状态 |
| Vue 前端 | 展示、交互、确认、任务进度、错误恢复 | 不保存 OIDC Token，不直接调用 Docker |
| WhaleDeck.Api | BFF、授权、业务用例、审计、Agent 调度 | 不直接挂 Docker Socket，不执行宿主机 Shell |
| WhaleDeck.Worker | 数据同步、周期采集、异步任务、告警计算 | 不直接获得宿主机 root 权限 |
| WhaleDeck.Agent | 主机采集、Docker 操作、受限 systemd 操作 | 不直接面向浏览器，不监听宿主机公网端口 |
| PostgreSQL | 持久化配置、审计和任务记录 | 不存 Authentik 密码或完整用户资料副本 |
| Valkey | 缓存、锁、短期任务状态 | 不作为唯一持久化来源 |

## 4. 用户角色与权限

角色由 Authentik 用户组映射，不在 WhaleDeck 内独立维护密码。

| 建议角色 | 权限范围 |
| --- | --- |
| `Viewer` | 查看概览、监控、系统信息、容器和任务状态 |
| `Operator` | Viewer + 启动、停止、重启普通容器，执行已批准备份 |
| `ApplicationAdmin` | Operator + 安装和更新应用、维护应用配置 |
| `PlatformAdmin` | 全部平台设置、用户组映射、数据库恢复、Docker 服务操作 |
| `Auditor` | 只读查看审计、登录、任务和配置变更记录 |

权限必须同时校验：用户角色、资源策略、操作类型和资源是否受保护。

## 5. 前端功能

### 5.1 启动、登录与会话

| 状态 | 功能 |
| --- | --- |
| `DONE` | 启动加载页，连接身份与平台服务 |
| `DONE` | 未登录用户不能进入主界面 |
| `DONE` | 登录页沿用 WhaleDeck 顶栏，仅显示品牌和主题切换 |
| `DONE` | 前端预览模式模拟登录 |
| `MVP` | 通过后端 OIDC Authorization Code + PKCE 跳转 Authentik |
| `MVP` | 用户名、密码和 MFA 仅由 Authentik 页面接收 |
| `MVP` | 登录成功返回原始页面，失效后回到登录页 |
| `MVP` | HttpOnly Cookie 会话，不在浏览器本地存储 Token |
| `P1` | 会话即将失效提示、主动续期和强制退出提示 |
| `P1` | 显示最近登录时间、来源 IP 和安全事件 |

### 5.2 全局界面框架

| 状态 | 功能 |
| --- | --- |
| `DONE` | 顶栏贯通全宽，滚动遮挡内容时才显示点阵遮罩 |
| `DONE` | Whale Deck 品牌、后端版本号、搜索入口、网络状态、通知和主题切换 |
| `DONE` | 固定侧边导航，不提供仅图标折叠模式 |
| `DONE` | 宽度不超过 1045px 时隐藏侧栏并显示展开按钮 |
| `DONE` | 用户信息位于侧栏左下角 |
| `DONE` | 明暗主题和减少动画模式 |
| `DONE` | 中英文等宽字体，区分 `O/0/I/l` |
| `MVP` | 全局搜索可定位页面、容器、数据库、任务和用户 |
| `MVP` | 面包屑、页面标题、权限不足和空状态统一规范 |
| `P1` | 顶栏任务中心：进行中、成功、失败、等待确认 |
| `P1` | 通知中心：资源告警、备份失败、更新待重启、容器异常 |

### 5.3 工作站概览

当前布局为单列，顺序固定为：资源总览 → 主机资源状态 → 实时监控 → 系统信息 → 热门应用。

#### 资源总览

| 状态 | 功能 |
| --- | --- |
| `DEMO` | 智能体数量和正在执行任务数 |
| `DEMO` | 网站/服务数量和可用数量 |
| `DEMO` | 数据库实例数量和网络隔离状态 |
| `DEMO` | 容器总量、运行量和停止量 |
| `MVP` | 点击卡片进入对应列表并保留筛选条件 |
| `MVP` | 异常资源使用警告色，不把“主机资源正常”和“服务已停止”混为一个状态 |

#### 主机资源状态

| 状态 | 功能 |
| --- | --- |
| `DEMO` | 主机资源健康度 |
| `DEMO` | CPU、内存、NVMe/HDD 使用率 |
| `MVP` | 数据来自 Agent，支持采样时间和数据新鲜度判断 |
| `MVP` | 70% 警告、85% 严重阈值，阈值可在设置中覆盖 |
| `P1` | 负载、温度、Swap、inode、磁盘健康和 GPU 状态 |

#### 实时监控

| 状态 | 功能 |
| --- | --- |
| `DEMO` | 网络流量总览，支持全部网卡和指定网卡 |
| `DEMO` | 磁盘 I/O 总览，支持全部磁盘和指定磁盘 |
| `MVP` | 实时采样与 1 小时、24 小时、7 天趋势 |
| `MVP` | 上下行、累计流量、读取、写入、IOPS、延迟 |
| `P1` | 配置采集间隔、保留天数、默认网卡和默认磁盘 |
| `P1` | 图表断线、Agent 离线和采集暂停的明确状态 |

#### 系统信息

| 状态 | 功能 |
| --- | --- |
| `DEMO` | 主机名称、发行版本、内核版本、系统类型 |
| `DEMO` | 内网地址、外网地址、启动时间、运行时间 |
| `DONE` | 内外网地址复制按钮 |
| `MVP` | Docker 版本、Agent 版本、CPU 型号、核心数、内存总量 |
| `P1` | BIOS、主板、GPU、磁盘型号与 SMART 摘要 |

#### 轩辕热门应用

| 状态 | 功能 |
| --- | --- |
| `DEMO` | Top 6 应用列表、图标、描述和推荐版本 |
| `DEMO` | 已安装应用显示运行状态、已装版本、关闭、启动、重启、更多和管理 |
| `DEMO` | 关闭与重启二次确认 |
| `MVP` | 后端从轩辕热门页获取、规范化并缓存数据 |
| `MVP` | 根据镜像、Compose 项目或标签判断是否已安装 |
| `P1` | 应用详情、版本选择、安装参数、依赖检查和安装任务进度 |
| `P1` | 镜像拉取走轩辕/现有镜像加速配置 |
| `P1` | 更新可用提示、变更预览、备份后更新和失败回滚 |

### 5.4 容器管理

功能参考 Portainer 的容器、镜像、网络、存储卷和 Stack 分层，但所有写操作都必须经过 WhaleDeck 权限和审计。

#### Docker 总览

- `MVP` Docker Engine 在线状态、版本、API 版本、运行时和存储驱动。
- `MVP` 容器、镜像、网络、卷、Compose 项目数量。
- `MVP` 运行、停止、异常、重启中和健康检查失败数量。
- `P1` 镜像占用、可回收空间和 Docker 数据目录容量。
- `P1` Docker 事件时间线。

#### 容器列表与详情

- `MVP` 搜索、状态筛选、Compose 项目筛选、标签筛选。
- `MVP` 名称、镜像、状态、健康状态、启动时间、端口、网络。
- `MVP` 启动、停止、重启；停止和重启需要二次确认。
- `MVP` 查看日志，支持时间、级别、关键字、尾部行数和实时追踪。
- `MVP` CPU、内存、网络和块 I/O 实时统计。
- `MVP` Inspect 信息只读展示，敏感环境变量默认脱敏。
- `P1` 重建、更新镜像、复制配置、导出诊断包。
- `P1` 查看挂载、网络、环境变量、标签、重启策略和健康检查。
- `P2` 经严格授权的容器终端；默认关闭，核心容器禁用。

#### 镜像

- `P1` 镜像列表、标签、摘要、大小、创建时间和引用容器。
- `P1` 从已批准 Registry 拉取镜像并显示进度。
- `P1` 检查镜像更新，不自动替换运行容器。
- `P1` 删除未使用镜像、悬空镜像和构建缓存，执行前显示预计回收空间。
- `P2` 镜像安全扫描结果和 SBOM 查看。

#### Compose 项目

- `P1` 识别现有 Compose 项目、服务、配置来源和工作目录。
- `P1` 启动、停止、重启、拉取和重新部署。
- `P1` 部署前显示配置差异和受影响容器。
- `P1` 失败时保留任务日志，不直接覆盖未知配置。
- `P2` Git 仓库驱动的配置同步和受控更新。

#### 网络与存储卷

- `MVP` 网络和卷只读列表、关联容器和占用关系。
- `P1` 创建与删除自定义网络；禁止修改 `database-platform-*` 内部网络边界。
- `P1` 卷容量、挂载点、备份状态和孤立卷识别。
- `P1` 删除前展示依赖图并要求再次输入资源名称。
- `P2` macvlan/ipvlan 高级配置，仅 PlatformAdmin 可用。

#### Docker 服务

- `MVP` 查看 `docker.service` 状态、最近启动时间和最近错误。
- `P1` 在 Agent 中实现受限的启动、停止、重启操作。
- `P1` 重启前展示将受影响的容器和预计恢复策略。
- `P1` Docker 停止后仍可通过宿主机 Agent 恢复，不依赖 Docker 内的 API 容器。

### 5.5 数据库与缓存平台

已部署资源以 `database-platform` 仓库为准：

| 类型 | 实例/用途 |
| --- | --- |
| SQL Server | 企业版实例 |
| PostgreSQL | 通用 PostgreSQL，WhaleDeck 和 Authentik 使用独立数据库及角色 |
| MariaDB | MySQL 开源兼容实现 |
| MongoDB | 文档数据库 |
| Valkey | 默认通用缓存，保持当前主版本 |
| Valkey 7.2 | 兼容旧应用的隔离实例 |

#### 前端功能

- `MVP` 实例卡片：类型、版本、容器状态、健康状态、运行时间和存储路径。
- `MVP` NVMe 数据容量和 HDD 备份容量。
- `MVP` 连接数、数据库数量、慢查询/错误摘要和最近备份。
- `MVP` 启动、停止、重启数据库容器，停止与重启二次确认。
- `MVP` 立即备份、查看备份、校验备份、下载审计信息。
- `P1` 恢复向导：选择备份、恢复目标、冲突检查、最终确认。
- `P1` 备份计划、保留策略和异地复制状态。
- `P1` 查看经过脱敏的连接信息；密码只允许重新生成或受控查看。
- `P1` 配置差异查看，不在第一版提供任意配置文件编辑器。
- `P2` 跳转专用管理工具，不在 WhaleDeck 内实现完整 SQL 工作台。

#### 后端约束

- 数据库端口不发布到宿主机，只允许明确加入内部网络的应用连接。
- WhaleDeck 通过 Agent 调用 `database-platform` 已有检查、备份和恢复脚本。
- 备份任务必须异步执行、可查询进度、可取消且产生审计记录。
- 恢复操作只允许 PlatformAdmin，并要求输入实例名或数据库名再次确认。
- 密钥、数据库密码和 SQL Server 产品密钥不得写入日志、数据库审计详情或 Git。

### 5.6 主机管理

功能参考 Cockpit 对 Linux 原生服务的集成方式：优先使用 systemd、journald、NetworkManager 和标准系统接口，不自行修改未知配置文件。

#### 系统与服务

- `MVP` 操作系统、内核、CPU、内存、负载、启动时间和时间同步状态。
- `MVP` systemd 服务列表、状态、启动方式和最近日志。
- `P1` 受限启动、停止和重启服务，核心服务标记为受保护。
- `P1` 进程列表、CPU/内存排序和进程详情。
- `P2` 结束进程；只允许 PlatformAdmin，并禁止直接结束 WhaleDeck Agent。

#### 软件更新

- `MVP` 展示 `database-platform-workstation-update.timer` 状态。
- `MVP` 展示每周日 20:00 的计划、上次结果、下次时间和待重启状态。
- `P1` 检查可用更新、查看包列表和执行普通更新。
- `P1` 更新任务不自动重启工作站。
- `P2` 配置维护窗口和人工批准的重启任务。

#### 日志

- `MVP` 查询 journald：时间、服务、优先级、关键字。
- `MVP` Docker 容器日志统一入口和下载诊断片段。
- `MVP` 默认限制时间范围、最大行数和响应体大小。
- `P1` 保存常用筛选、错误聚合和关联到容器/任务。
- `P1` 日志清理只调用既有轮转策略，不直接清空未知文件。

#### 网络

- `MVP` 两张网卡的地址、链路状态、速率、流量和路由摘要。
- `MVP` 清楚标识内网网卡和具备外网能力的网卡。
- `P1` DNS、默认路由、监听端口和防火墙只读视图。
- `P2` 网络和防火墙写操作；必须提供回滚计时器，避免远程锁死。

#### 磁盘与文件系统

- `MVP` NVMe、HDD、分区、文件系统、挂载点、容量和 inode。
- `MVP` 数据目录与备份目录的独立健康检查。
- `P1` SMART 健康、温度、累计通电和错误摘要。
- `P1` 大目录扫描、清理建议和预计回收空间。
- `P2` 受限文件浏览器，仅开放批准目录，不允许浏览 `/` 全盘。

#### 终端

- `HOLD` 首期不提供任意 Web Shell。
- `P2` 如启用，必须使用短时授权、命令审计、会话超时和角色限制。
- `P2` 优先提供预定义运维动作，而不是让用户直接输入 Shell 命令。

### 5.7 网站与内部服务

WhaleDeck 当前不是域名托管面板，“网站”首先表示可访问的内部 Web 服务。

- `MVP` 服务目录：WhaleDeck、Authentik、GitLab 及后续内部应用。
- `MVP` 名称、IP 入口、健康状态、延迟、版本和所属容器。
- `MVP` 从当前网络安全打开服务入口。
- `P1` HTTP 健康检查、依赖关系和最近故障。
- `P1` GitLab 部署状态、版本和基本运行指标。
- `P2` 反向代理路由的受控配置。
- `HOLD` 域名、HTTPS 证书和公网发布。

### 5.8 智能体与任务

- `MVP` 智能体列表、在线状态、版本、当前任务和最近活动。
- `MVP` 任务列表：排队、运行、成功、失败、取消和等待确认。
- `MVP` 任务详情：发起用户、目标资源、开始/结束时间、日志摘要和结果。
- `P1` 取消可取消任务、重试失败任务和关联审计记录。
- `P1` Agent 心跳、版本不一致和能力协商。
- `P2` 多 Agent 节点，但不等同多宿主机 Docker 集群。

### 5.9 用户、用户组与 SSO

- `MVP` 当前用户信息、Authentik Subject、用户组和 WhaleDeck 角色。
- `MVP` Authentik 用户/组只读同步，不建立重复用户资料表。
- `MVP` 登录、退出、禁用用户后的会话处理。
- `P1` 通过 Authentik 管理 API 创建、更新、禁用用户；WhaleDeck 只做代理和审计。
- `P1` WhaleDeck 角色与 Authentik 用户组映射。
- `P1` GitLab 用户和组同步任务、差异预览和结果报告。
- `P1` SSO 应用目录：Authentik Provider、回调地址、状态和最近错误。
- `P2` 多应用统一授权申请和审批。

### 5.10 审计、告警与设置

#### 审计

- `MVP` 登录、退出、权限拒绝、容器操作、备份恢复和设置修改。
- `MVP` 记录用户、时间、来源 IP、资源、动作、结果和关联任务 ID。
- `MVP` 审计事件不可从普通界面修改。
- `P1` 条件查询、导出和保留策略。

#### 告警

- `MVP` CPU、内存、磁盘、Agent 离线、容器异常和备份失败。
- `MVP` 页面通知和未读状态。
- `P1` 去重、静默、恢复通知和维护窗口。
- `P1` 邮件、Webhook 或钉钉等外部通知渠道，由用户后续选择。

#### 设置

- `MVP` 只读显示运行版本、环境、Agent 连接和外部依赖。
- `MVP` 阈值、采集间隔、保留时间和默认网卡/磁盘。
- `P1` 轩辕镜像服务、Registry 和镜像更新策略。
- `P1` 数据备份保留策略、异地复制目标和维护窗口。
- `P1` Authentik 组到 WhaleDeck 角色映射。
- 设置页面只显示 Secret 是否已配置，绝不回显完整 Secret。

## 6. 后端功能模块

### 6.1 WhaleDeck.Api

| 状态 | 模块 | 责任 |
| --- | --- | --- |
| `DONE` | Authentication | Cookie + OIDC、PKCE、Authentik 登录和退出 |
| `DONE` | System | API 存活状态 |
| `MVP` | CurrentUser | 用户、组、角色和权限集合 |
| `MVP` | Overview | 聚合资源、监控、系统信息和热门应用 |
| `MVP` | Host | 主机信息、指标、服务、更新和日志查询 |
| `MVP` | Containers | Docker 查询和受控生命周期操作 |
| `MVP` | Databases | 数据库实例、健康、备份和恢复任务 |
| `MVP` | Applications | 轩辕热门应用、安装状态和安装任务 |
| `MVP` | Jobs | 异步任务状态、进度、取消和重试 |
| `MVP` | Audit | 审计查询和导出 |
| `P1` | UsersAndSso | Authentik 与 GitLab 用户/组同步 |
| `P1` | Notifications | 告警、通知和未读状态 |
| `P1` | Settings | 动态平台设置和安全配置状态 |

控制器只处理 HTTP 传输；业务规则进入 `Application`，外部系统和持久化进入 `Infrastructure`。

### 6.2 WhaleDeck.Agent

建议作为独立 .NET Worker 安装为宿主机 systemd 服务，通过 Unix Socket 提供内部 gRPC 接口。

#### 只读能力

- 主机、操作系统、内核、CPU、内存、负载和运行时间。
- 网卡、地址、路由、流量和链路状态。
- 磁盘、文件系统、挂载点、I/O 和 SMART 摘要。
- systemd 服务与 timer 状态。
- journald 查询。
- Docker Engine 信息、容器、镜像、网络、卷、事件、统计和日志。
- `database-platform` 检查脚本结果。

#### 写操作

- 启动、停止和重启容器。
- 拉取镜像和受控 Compose 重部署。
- 执行批准的数据库备份/恢复脚本。
- 执行普通系统更新。
- 启动、停止或重启白名单内 systemd 服务。
- 写操作必须携带用户、任务 ID、资源 ID、动作、幂等键和授权声明。

#### Agent 安全边界

- Unix Socket 位于 `/run/whaledeck/agent.sock`，不开放 TCP 端口。
- 仅 WhaleDeck API 所属组可连接。
- 每个 RPC 使用明确 DTO，不提供任意 Shell 字符串参数。
- 路径使用服务器端注册的资源 ID，不接受任意绝对路径。
- 容器按标签和保护策略校验；核心容器默认受保护。
- Docker、网络、SSH 和 WhaleDeck 自身相关操作使用更高权限等级。
- Agent 记录结构化审计，但避免重复写入高频指标日志。

### 6.3 WhaleDeck.Worker

- `MVP` Agent 心跳和能力同步。
- `MVP` 指标采集、汇总、降采样和过期清理。
- `MVP` Docker 事件订阅和容器状态缓存。
- `MVP` 轩辕热门应用抓取与缓存。
- `MVP` 异步任务调度、租约、超时、取消和失败重试。
- `MVP` 备份结果同步和告警生成。
- `P1` Authentik/GitLab 用户组同步。
- `P1` 镜像更新检查和系统更新检查。
- `P1` 通知投递与恢复通知。

### 6.4 数据持久化建议

#### PostgreSQL

- `AuditEvent`：不可变审计事件。
- `PlatformSetting`：非敏感动态设置。
- `OperationJob`：异步任务、进度、结果和幂等键。
- `ManagedResource`：资源 ID、类型、标签和保护策略。
- `MetricSample`/`MetricRollup`：历史指标或其聚合值。
- `AlertRule`、`AlertEvent`：告警规则、触发和恢复。
- `RoleMapping`：Authentik 组到平台角色映射。
- `ExternalSyncRun`：Authentik/GitLab 同步历史。
- `BackupRecord`：备份文件、校验、大小、保留期和恢复记录。

不建立本地密码表，不复制 Authentik 的姓名、邮箱等完整用户资料。

#### Valkey

- 概览快照和热门应用缓存。
- Agent 心跳和容器短期状态。
- 分布式锁、幂等键和任务进度。
- SSE/WebSocket 通知的临时状态。
- 频率限制和短期权限缓存。

Valkey 数据丢失后系统必须能够从 PostgreSQL、Agent 和外部服务恢复。

## 7. 建议 API 分组

```text
/api/v1/auth/*
/api/v1/me
/api/v1/overview
/api/v1/host/info
/api/v1/host/metrics
/api/v1/host/services
/api/v1/host/logs
/api/v1/host/updates
/api/v1/docker/info
/api/v1/containers/*
/api/v1/images/*
/api/v1/networks/*
/api/v1/volumes/*
/api/v1/compose-projects/*
/api/v1/databases/*
/api/v1/backups/*
/api/v1/applications/*
/api/v1/agents/*
/api/v1/jobs/*
/api/v1/users/*
/api/v1/sso/*
/api/v1/audit-events
/api/v1/alerts/*
/api/v1/settings/*
```

写操作统一返回异步任务 ID；前端通过 SSE 或轮询获取进度。错误响应使用 Problem Details，并提供可检索的关联 ID。

## 8. 操作安全等级

| 等级 | 示例 | 前端要求 | 后端要求 |
| --- | --- | --- | --- |
| L0 只读 | 查看指标、日志、详情 | 无确认 | Viewer 可用 |
| L1 可恢复 | 启动、重启、立即备份 | 普通确认 | Operator + 审计 |
| L2 有中断 | 停止容器、重启服务、更新应用 | 二次确认并展示影响 | ApplicationAdmin/PlatformAdmin |
| L3 数据风险 | 删除、恢复、清理卷、网络修改 | 输入资源名称确认 | PlatformAdmin + 独占锁 + 完整审计 |
| L4 主机风险 | Docker 停止、网络修改、系统重启 | 维护窗口和再次认证 | PlatformAdmin + Agent 白名单 |

受保护资源至少包括：WhaleDeck Gateway/API/Worker、WhaleDeck Agent、Authentik、PostgreSQL、Valkey 和关键数据库实例。

## 9. 非功能要求

### 安全

- 默认拒绝，显式授权。
- 所有写操作服务端再次校验，不能依赖前端隐藏按钮。
- 防止 CSRF、开放重定向、越权访问和资源 ID 猜测。
- 敏感字段默认脱敏，不写日志，不进入错误详情。
- API、Agent、数据库和缓存不直接发布宿主机端口。

### 可用性

- Agent 离线时前端进入明确的只读降级状态。
- 外部依赖失败不能阻止查看最近一次有效快照。
- 长任务可恢复、可查询且不会因浏览器关闭而中断。
- Docker 停止后宿主机 Agent仍可工作并恢复 Docker。

### 性能

- 概览 API 目标响应时间小于 500ms，使用 Valkey 快照避免现场串行探测。
- 高频指标先在 Agent 聚合，再批量写入；不把每秒采样全部写入应用日志。
- 容器日志和 journald 查询必须限制时间、行数和返回大小。

### 日志

- 应用只向 stdout/stderr 输出结构化 JSON。
- 正常请求低于错误级别，避免重复发生日志打满磁盘事故。
- Docker 使用有界 `local` 日志驱动和轮转。
- 审计事件与运行日志分离。

### 前端体验

- 字号基线：指标总量 28–32px；模块标题 16px；卡片标题 14px；正文/键值 13px；辅助文字 12px。
- 颜色不是唯一状态表达，必须同时提供文字或图标语义。
- 危险操作统一确认、结果提示和任务入口。
- 支持键盘焦点、可访问名称和减少动画。
- 小屏优先展示状态和主要操作，次要信息进入详情页。

## 10. 分阶段实现建议

### 阶段 A：真实只读数据（MVP-1）

1. 新建 WhaleDeck.Agent 和 Unix Socket 通信。
2. 接入真实主机、Docker、网卡、磁盘和系统信息。
3. 完成 `/api/v1/overview` 聚合接口。
4. 将当前演示概览替换为真实数据，保留数据新鲜度和离线状态。
5. 建立 Docker 事件订阅、Agent 心跳和 Valkey 快照。

### 阶段 B：安全容器操作（MVP-2）

1. Authentik 组到 WhaleDeck 角色映射。
2. 容器列表、详情、日志和统计。
3. 启动、停止、重启和任务中心。
4. 资源保护标签、二次确认和审计。
5. Docker 服务状态和 Agent 降级处理。

### 阶段 C：数据库与备份（MVP-3）

1. 六套数据库/缓存实例概览。
2. 接入现有检查、备份和恢复脚本。
3. 备份记录、校验、保留策略和失败告警。
4. NVMe/HDD 容量与 85% 阈值告警。

### 阶段 D：应用和系统运维（P1）

1. 轩辕热门应用真实数据与安装向导。
2. 镜像、Compose、网络和卷管理。
3. systemd、journald、软件更新和磁盘健康。
4. 告警、通知渠道和维护窗口。

### 阶段 E：用户、GitLab 与 SSO（P1）

1. Authentik 用户/组管理代理。
2. GitLab 部署与服务目录。
3. GitLab 用户和组同步。
4. SSO 应用状态、失败诊断和审计。

## 11. 参考面板及取舍

| 参考项目 | 借鉴内容 | WhaleDeck 的取舍 |
| --- | --- | --- |
| [1Panel](https://1panel.cn/docs/v1/index.html) | 主机监控、数据库、容器、应用商店、备份与恢复的功能分组 | 保留清晰分组和应用安装体验，不直接复制网站/域名优先模型 |
| [1Panel 主机监控](https://proxy2.1panel.cn/docs/v2/user_manual/hosts/monitor/) | CPU、内存、磁盘 I/O、网络 I/O、设备筛选、采集间隔和保留期 | 首期聚焦单机，默认配置更保守，避免监控数据占满数据库 |
| [Cockpit](https://cockpit-project.org/guide/latest/features.html) | 使用 systemd/journald、NetworkManager、storaged 和 PolicyKit 等 Linux 原生能力 | Agent 优先调用稳定系统接口，不随意改写配置文件 |
| [Portainer 容器管理](https://docs.portainer.io/user/docker/containers) | 容器详情、Inspect、日志、统计、控制台、镜像更新提示 | 保留查看与生命周期管理，控制台和主机能力默认关闭 |
| [Portainer 网络](https://docs.portainer.io/user/docker/networks) | bridge、macvlan、ipvlan、overlay 等网络视图 | 首期只读，后续写操作严格保护数据库内部网络 |
| [Portainer 高级容器设置](https://docs.portainer.io/user/docker/containers/advanced) | 挂载、网络、日志驱动、资源限制、GPU 和能力配置 | 高风险字段只对高级管理员开放，并显示等效变更和影响 |
| [Webmin 系统日志](https://webmin.com/docs/modules/system-logs/) | 日志来源、级别和查询方式 | 使用 journald 与容器日志，不提供任意日志目标配置 |
| [Webmin 计划任务](https://webmin.com/docs/development/creating-scheduled-cron-jobs/) | 查看、执行和维护计划任务 | 优先管理 WhaleDeck 注册的 systemd timer，不默认开放所有用户 crontab |

## 12. 待用户补充与确认

请在后续补充时优先确认以下内容：

- [ ] 实际使用角色及每个角色允许执行的操作。
- [ ] 哪些容器永远禁止从 WhaleDeck 停止、重建或删除。
- [ ] 是否允许 WhaleDeck 管理 Docker 服务本身。
- [ ] 是否需要容器终端；哪些用户、哪些容器可以使用。
- [ ] 数据库备份保留天数、HDD 容量阈值和异地备份目标。
- [ ] 数据库恢复是否允许覆盖原数据库，还是只能恢复为新名称。
- [ ] 轩辕应用安装模板的来源、审核方式和允许的镜像 Registry。
- [ ] GitLab 的部署时间、版本策略和用户/组同步规则。
- [ ] 是否需要文件管理器；若需要，可访问哪些固定目录。
- [ ] 是否需要防火墙和网络写操作。
- [ ] 告警渠道：页面、邮件、Webhook、钉钉或其他方式。
- [ ] 指标采集间隔和历史数据保留时间。
- [ ] 系统普通更新之外，是否允许自动更新容器镜像。
- [ ] 是否需要审批流，还是管理员二次确认即可执行高风险操作。

本文在以上问题确认后可继续拆分为：页面清单、后端用例、Agent RPC 契约、权限矩阵和分阶段开发任务。
