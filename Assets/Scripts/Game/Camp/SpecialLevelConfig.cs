using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LitJson;

namespace Camp
{
    /// <summary>
    /// 特殊模式单个节点配置
    /// </summary>
    [Serializable]
    public class SpecialNodeConfig
    {
        public string type;             // "battle", "eliteBattle", "boss", "shop", "event", "hotSpring", "wish"
        public int[] enemies;           // 敌方单位ID列表（战斗类节点）
        public float difficulty = 1.0f; // 难度系数（属性乘算）
    }

    /// <summary>
    /// 特殊模式单层配置
    /// </summary>
    [Serializable]
    public class SpecialLayerConfig
    {
        public int layer;
        public SpecialNodeConfig node1;
        public SpecialNodeConfig node2; // null 表示该层只有1个节点
    }

    /// <summary>
    /// 特殊模式关卡配置加载器
    /// </summary>
    public class SpecialLevelConfig
    {
        private List<SpecialLayerConfig> _layers = new List<SpecialLayerConfig>();

        public int LayerCount => _layers.Count;

        public void Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Tables", "special_levels_config.json");
            if (!File.Exists(path))
            {
                Debug.LogError("[SpecialLevelConfig] Config file not found: " + path);
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                JsonData root = JsonMapper.ToObject(json);
                JsonData layersJson = root["layers"];

                _layers.Clear();
                for (int i = 0; i < layersJson.Count; i++)
                {
                    var layerCfg = new SpecialLayerConfig
                    {
                        layer = (int)layersJson[i]["layer"],
                        node1 = ParseNode(layersJson[i]["node1"]),
                        node2 = layersJson[i].ContainsKey("node2") && layersJson[i]["node2"] != null
                            ? ParseNode(layersJson[i]["node2"])
                            : null
                    };
                    _layers.Add(layerCfg);
                }

                Debug.Log($"[SpecialLevelConfig] Loaded {_layers.Count} layers");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpecialLevelConfig] Failed to load: {e.Message}");
            }
        }

        public SpecialLayerConfig GetLayer(int layer)
        {
            if (layer < 1 || layer > _layers.Count) return null;
            return _layers[layer - 1];
        }

        public List<SpecialLayerConfig> GetAllLayers()
        {
            return _layers;
        }

        private SpecialNodeConfig ParseNode(JsonData json)
        {
            if (json == null) return null;

            var config = new SpecialNodeConfig
            {
                type = json.ContainsKey("type") ? json["type"].ToString() : "battle",
                difficulty = json.ContainsKey("difficulty") ? (float)(double)json["difficulty"] : 1.0f
            };

            if (json.ContainsKey("enemies") && json["enemies"].IsArray)
            {
                config.enemies = new int[json["enemies"].Count];
                for (int i = 0; i < json["enemies"].Count; i++)
                    config.enemies[i] = (int)json["enemies"][i];
            }

            return config;
        }
    }
}
