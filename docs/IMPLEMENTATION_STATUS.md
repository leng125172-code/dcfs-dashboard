# 实施状态与恢复入口

截至 2026-10-09（Asia/Shanghai），完整后端计划仍在实施中。构建通过不等于所有业务与远程验收完成。

## 工作站恢复后的只读检查

工作站离线期间暂停了 SSH、部署、重启和清理。用户确认重新连接后，2026-10-09 18:25（Asia/Shanghai）的只读检查确认：8 个基础容器仍健康、旧容器停止、SHA256SUMS 校验通过、两个 IP 在线，磁盘容量充足。远程配置仓库仍为 `06e0e94`，未提交的 `.migration-state/` 属于迁移状态，不删除或提交。

SSH 用户没有非交互 sudo，符合不开放 `NOPASSWD:ALL` 的约定。安装宿主服务及切换更新 timer 需要一次交互式 sudo；不得将已提供的密码写入脚本、命令输出或日志。

离线前最后一次成功记录：

- 8 个 `database-platform-*` 基础容器健康，Whale Deck PostgreSQL 数据库/角色及 Valkey ACL 已初始化。
- 旧容器保持停止状态，旧网络保留；未执行最终删除。
- 安全备份：`/data/DockerData/migration-dcfs-20261009_163559`。
- 保留 `/data/GitRepos/DCFS -> /data/GitRepos/database-platform` 兼容链接用于旧容器配置挂载回滚。仅在完成验收和旧容器清理后移除此链接。
- Agent、MaintenanceHost 尚未安装验收；新名称更新 timer 尚未安装验收。
- 本次未构建、启动或推送 Whale Deck 容器镜像。

## 当前本地实现

- Contracts、Agent、MaintenanceHost、Application、Infrastructure、API、Worker 工程及 EF 初始 migration。
- Swagger UI / 内置 OpenAPI、门户、基础查询、容器生命周期、任务/Outbox 骨架与权限快照。
- 七阶段安装器及纯预览、准备、依赖安装入口；一次性 Migrator；独立宿主服务发布目录。
- 配置复用、加速源合并/验证/回滚、Git origin/脏工作区检查、安装互斥锁、镜像摘要部署检查。

## 本地验证记录

- .NET Release 构建通过：0 警告、0 错误。
- .NET 自动化测试 15 项通过（10 项 UnitTests、5 项 IntegrationTests；其中身份测试使用离线 HTTP/cache 替身）。
- 安装器命令行 5 项检查通过，空 PATH 下验证 dry-run 不调用系统工具。
- 依赖环境生成测试通过：专用密码独立生成，重复执行不覆盖原文件。
- Shell 语法检查通过；未执行正式安装流程或容器镜像构建。

## 仍需实现或实测

下列项目不能标记为完成：六类数据库管理适配器与真实 CRUD/授权/轮换验收，应用安装/更新/回滚执行器，备份与定时调度，完整资源锁与任务恢复，指标日分区/聚合，告警和配置推送，完整 Authentik 用户与 SSO 写操作，Agent peer credentials 和 capability 防重放，Docker 配置维护的端到端回滚。

宿主服务安装/升级、systemd 沙箱与受限 sudo helper 的兼容性仍需 Linux 实测；Authentik 的 OIDC subject 与管理 API 用户 ID 映射仍需完善。不能据当前脚本认定生产认证或宿主高权限操作已完成验收。

部分管理端点与 RPC 目前只是受控入队或注册信息查询，不能当作上述业务已可执行。现有测试包括离线替身测试，不代表 PostgreSQL、Valkey、Docker 或 Authentik 真实依赖集成测试已通过。

## 连接恢复后的顺序

1. 只读检查当前容器、挂载、备份校验清单和 Git 状态。
2. 对比并同步离线期间的脚本变更，复核 ACL 和专用数据库权限。
3. 安装受限 helper、Agent、MaintenanceHost 和新 timer，验证 UDS/进程权限与双网卡入口。
4. 临时测试资源上的集成验收和维护恢复验证。
5. 完整验收通过后才能清理旧容器对象/旧网络，保留所有 NVMe/HDD 数据。

API/Worker 不临时安装成 systemd 服务。生成正式镜像与正式部署在安装脚本中定义，当前开发任务不执行镜像阶段。
