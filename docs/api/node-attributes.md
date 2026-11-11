# 节点语义属性与字段规则

以下路径省略 `/api/v1`，需有效会话，网页使用同源 Cookie，原生和外部客户端使用 Bearer。管理节点属性与规则需要管理员。能力标识 `organize.node_attributes`。节点属性是归纳输入，作品归属、播放组和字幕关联仍是保存后的派生结果。

## 属性字段与来源前缀

节点 `attributes` 和属性详情的 `fields` 都包含以下本地值，每个值独立编码为 `来源:内容`。`auto:` 可被扫描或重建替换；`manual:`、`ai:` 持久保护当前字段，不冻结节点的其他字段或整部作品。

| 字段 | 内容取值 | 含义 |
| --- | --- | --- |
| boundary | infer / root / not_work / exclude / assign | infer 自动判断；root 强制作品根；not_work 不在当前目录建根；exclude 排除节点/子树；assign 指定文件作品 |
| identifier | none 或大写字母数字编号，多个以逗号隔开 | 作品编号线索，不等于作品唯一身份 |
| format | none 或 1..16 位小写字母数字 | 格式；例如 wav、mp3。目录是提示，文件是分类结果 |
| media_kind | none / audio / video | 文件是否参与音视频分组；目录不能人工设置此字段 |
| category | none / main / bonus / video / other | 播放内容类别 |
| effect | none / se / no_se | 效果版本；none 在播放分类中对应 default |
| transparent | true / false | 目录是透明包装，不增加章节名称 |
| collection | true / false | 集合容器提示，用于结构判断；完整归档时同步生成归档收藏集 |
| ignore | true / false | 忽略节点，目录会阻断整个子树 |

默认 boundary 为 auto:infer；编号/格式/媒体类型/类别/效果均为 auto:none，三个布尔属性为 auto:false。扫描根据规则更新自动值。`auto:infer` 不说明这个目录最终不是作品根：标准 RJ 目录可由编号和结构推导为作品，最终边界用作品归属查询确认。root 属性表达显式边界指令，而非复制一份最终关系。

目录类别、效果和格式提示向后代传递，遇到新作品根重置为该根的本地分类。格式优先采用文件本地结果，缺少文件格式时才使用目录提示；显式文件 format=manual:none 阻止该回退。人工类别覆盖继承；manual:none 类别重置为默认 main，效果重置为 default。自动类别保留特典父目录的语义，文件本地类别可覆盖父目录类别。默认类别规则同时检查文件名，例如 extra.mp3 可归 bonus；可通过规则 scope 限制为仅目录或文件。所有节点仍最多属于一个作品。

## 查询与批量修改

`GET /sources/{source}/attributes/{node}` 返回 200 和 ETag：

```json
{
  "revision": "42",
  "node": {
    "node_id": "node-id",
    "work_id": "",
    "fields": {
      "boundary": "auto:infer",
      "identifier": "auto:none",
      "format": "auto:wav",
      "media_kind": "auto:none",
      "category": "manual:main",
      "effect": "auto:none",
      "transparent": "auto:false",
      "collection": "auto:false",
      "ignore": "auto:false"
    }
  }
}
```

返回已保存的本地属性，不在 GET 时重新计算继承；自动值在扫描或归纳快照前的独立输入维护中更新。来源必须启用且未退役，节点须属于该来源，否则 404；已知 missing 节点可编辑。

`POST /sources/{source}/attributes` 携带上述 ETag 的 If-Match，原子修改 1..1000 个节点，每个节点最多一项：

```json
{
  "edits": [
    {"node_id":"bonus-directory","fields":{"category":"manual:main","effect":"manual:no_se"}},
    {"node_id":"one-file","fields":{"effect":"manual:none"}},
    {"node_id":"other-directory","fields":{"category":null}}
  ]
}
```

省略字段保持原值；非 null 值只接受 manual:/ai:；null 表示解除保护，立即根据当前规则和当前已登记名称/种类重新分类这个字段。它不恢复历史自动值，也不影响其他字段。每项至少提供一个 fields 键或 work_id；不是完整覆盖 fields 对象。

root/assign 必须配合已有目标 work_id；目标先用作品接口创建。设置其他边界或将 boundary 设 null 自动清空目标。work_id 是普通作品身份字符串，不附来源前缀。已有另一个根时须同批明确解除旧根，不能隐式抢占；允许最终图中的根交换。字段取值/节点种类错误 400，未知节点/目标 404，排除/范围/重复根冲突 409，缺 If-Match 为 428，修订过期 412，整批失败无部分写入。

成功返回 revision、pending=true、edit_count 和新 ETag，完全相同的请求不改变修订。ETag 使用来源输入代次，该来源扫描、规则和节点属性变化会使它失效；普通作品描述编辑与关系发布不会使它失效。直接执行归纳或开启 auto_organize 后由后台切换成员和分组；保存属性本身不改变已发布关系。归纳若遇锁定或索引不完整，属性仍保留，原关系整体保留。

原 `/decisions`、`/decisions/batch`、根迁移、作品 release 和显式文件归属接口操作同一个 boundary 字段，不存在另一份优先级更高的决定。解除边界不会清除 category/effect 等人工字段。已有作品成员/默认组的显式播放覆盖仍独立保留；若希望节点属性接管其分类，先按 [纠正接口](curation.md) 撤销相应播放覆盖。

## 正则按目标字段分组

`GET /organize/rules` 返回 `{revision,profile}` 和 rules ETag；`PUT /organize/rules` 带该 ETag完整替换 profile。旧的扁平 rules/category 配置已移除。省略某字段组等同无规则，空 fields 合法；需要保留默认规则时，读取现有配置后只修改对应组。

```json
{
  "fields": {
    "category": [
      {"id":"bonus-label","target":"label","scope":"any","pattern":"(?i)(特典|おまけ|bonus)","value":"bonus","priority":100,"enabled":true}
    ],
    "format": [
      {"id":"wav-directory","target":"label","scope":"directory","pattern":"(?i)^wav$","value":"wav","priority":100,"enabled":true},
      {"id":"audio-extension","target":"extension","scope":"file","pattern":"^(wav|mp3)$","value":"$1","priority":100,"enabled":true}
    ],
    "media_kind": [
      {"id":"audio-kind","target":"extension","scope":"file","pattern":"^(wav|mp3)$","value":"audio","priority":100,"enabled":true}
    ]
  }
}
```

组名就是被赋值的字段，共上述九组。规则字段 id、target、scope、pattern、value、priority、enabled；id 全配置唯一，最多 128 条，pattern 最多 4096 字节。scope 为 any/file/directory，省略或空为 any；target 为 name/label/extension。匹配副本执行 NFKC/小写，label 额外处理前导序号和下划线，extension 无点；不改原名。

同组按 priority 降序、id 升序，采用首个有效匹配；identifier 收集边界合法的匹配、转大写并去重排序。identifier、format 可展开捕获，枚举字段使用合法字面值。boundary 正则允许 infer/root/not_work/exclude，不允许 assign；assign 需要人工目标作品。规则值不写 auto: 前缀，由分类器添加。

正则使用 RE2 语法，不支持反向引用和环视。无效正则、捕获引用、字段或枚举值在保存时拒绝，不替换旧配置。未启用的草稿规则也验证表达式，运行时跳过。规则只能识别本地线索；目录归属仍遵守最近作品根、同号包装及子作品边界规则。

当前 FANZA 支持暂停；内置 `fanza-code` 规则保留定义但不生效。GET 返回其 `enabled=false`，PUT 中该内置规则同样规范化为关闭；其他规则保持原配置。不会删除既有作品编号或源目录。
