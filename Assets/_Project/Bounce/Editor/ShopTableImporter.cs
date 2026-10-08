using System.Collections.Generic;
using System.Reflection;
using BK.Data;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace BK.Kit.Editor
{
    public static class ShopTableImporter
    {
        [System.Serializable] private sealed class ProductSource { public List<ShopProductRow> _rows; }
        [System.Serializable] private sealed class CurrencySource { public List<CurrencyDefinitionRow> _rows; }
        private const string Root="Assets/_Project/Bounce/Content/Tables/";
        [MenuItem("BK/Integration/Import Shop Tables")]
        public static void Import()
        {
            var products=ScriptableObject.CreateInstance<ShopProductTable>();
            var currencies=ScriptableObject.CreateInstance<CurrencyDefinitionTable>();
            try
            {
                typeof(TableAsset<int,ShopProductRow>).GetField("_rows",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(products,JsonUtility.FromJson<ProductSource>(File.ReadAllText(Root+"ShopProductTable.json"))._rows);
                typeof(TableAsset<string,CurrencyDefinitionRow>).GetField("_rows",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(currencies,JsonUtility.FromJson<CurrencySource>(File.ReadAllText(Root+"CurrencyDefinitionTable.json"))._rows);
                var catalog=new ShopCatalog(products,currencies);
                foreach(var booster in BoosterCatalog.All)catalog.BoosterPrice(booster.Kind);
                var productAsset=AssetDatabase.LoadAssetAtPath<ShopProductTable>(Root+"ShopProductTable.asset");
                var currencyAsset=AssetDatabase.LoadAssetAtPath<CurrencyDefinitionTable>(Root+"CurrencyDefinitionTable.asset");
                EditorUtility.CopySerialized(products,productAsset);productAsset.name="ShopProductTable";
                EditorUtility.CopySerialized(currencies,currencyAsset);currencyAsset.name="CurrencyDefinitionTable";
                EditorUtility.SetDirty(productAsset);EditorUtility.SetDirty(currencyAsset);AssetDatabase.SaveAssets();
                Debug.Log("BK_SHOP_TABLES_OK: "+catalog.VisibleProducts().Count()+" lobby products");
            }
            finally { Object.DestroyImmediate(products);Object.DestroyImmediate(currencies); }
        }
    }
}
