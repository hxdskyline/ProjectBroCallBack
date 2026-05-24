# AvatarTemp 资源清单

每个资源为 PNG 序列帧，`xxx1.png` = 第 1 帧（Idle/Run/Death），`xxx2.png` = 第 2 帧（Attack）。

## 已绑定 fighter_config（avatarId 字段）

| 文件名 | 汉字 | 对应单位 | fighterId |
|---|---|---|---|
| youxia | 游侠 | 狸花猫族长 | 1000 |
| maomao | 矛猫 | 狸花T1 | 1001 |
| maoqishi | 猫骑士 | 狸花T2 暗影刺客 | 1002 |
| lihua | 狸花 | 狸花T3 狩猎大师 | 1003 |
| dajuleader | 大橘族长 | 橘猫族长 | 2000 |
| jituiyongshi | 鸡腿勇士 | 橘猫T1 | 2001 |
| cangying | 苍蝇 | 橘猫T2 蝇群 | 2002 |
| jiaorouche | 绞肉车 | 奶牛T2 绞肉机 | 3002 |
| daju | 大橘 | 橘猫T3 熔炉 | 2003 |
| nainiuleader | 奶牛族长 | 奶牛猫族长 | 3000 |
| nainiu | 奶牛 | 奶牛T1 侍僧 | 3001 |
| xianluo | 暹罗 | 暹罗猫族长 / 暹罗T3 灵能化身 | 4000, 4003 |
| lingnengxuetu | 灵能学徒 | 暹罗T1 | 4001 |
| longyishushi | 龙裔术士 | 暹罗T2 | 4002 |

## 敌人

| 文件名 | 汉字 | fighterId |
|---|---|---|
| panglaoshu | 胖老鼠 | 5000 |
| laoshu | 老鼠 | （待配置） |
| dalaoshu | 大老鼠 | （待配置） |

## 未绑定 fighter_config（备用素材）

| 文件名 | 汉字 |
|---|---|
| hanbaomao | 汉堡猫 |
| hunhun | 混混 |
| maoqiguan | 猫气罐 |
| maozai | 猫仔 |
| reliangzhadan | 热量炸弹 |

## 子文件夹

- `Hero/` — 英雄动画（Attack, Idle）
- `Enemy/` — 敌人动画（Attack, Die, Idle, Run）

## 命名规则

- `拼音 + 1` = 第 1 帧（正面/待机）
- `拼音 + 2` = 第 2 帧（侧面/攻击）
- `拼音 + leader` = leader 专用变体（比普通版更精致）
