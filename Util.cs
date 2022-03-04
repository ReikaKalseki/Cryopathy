/*
 * Created by SharpDevelop.
 * User: Reika
 * Date: 02/03/2022
 * Time: 11:54 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace ReikaKalseki.Cryopathy
{
	public class Util
	{
		public static void log(string s) {
			Debug.Log("CRYOPATHY: "+s);
		}
		
		public static void dropItem(long x, long y, long z, string name) {
			if (ItemEntry.mEntriesByKey.ContainsKey(name)) {
		    	ItemBase item = ItemManager.SpawnItem(ItemEntry.mEntriesByKey[name].ItemID);
		    	DroppedItemData stack = ItemManager.instance.DropItem(item, x, y, z, Vector3.zero);
	    	}
	    	else {
	    		log("NO SUCH ITEM TO DROP: "+name);
	    	}
		}
		
		public static void modifyIngredientCount(CraftData rec, string item, uint newAmt) {
			foreach (CraftCost ing in rec.Costs) {
				if (ing.Key == item) {
					ing.Amount = newAmt;
					log("Changed amount of "+item+" to "+newAmt+" in recipe "+recipeToString(rec, true));
				}
			}
		}
		
		public static void removeIngredient(CraftData rec, string item) {
			for (int i = rec.Costs.Count-1; i >= 0; i--) {
				CraftCost ing = rec.Costs[i];
				if (ing.Key == item) {
					rec.Costs.RemoveAt(i);
					log("Removed "+item+" from recipe "+recipeToString(rec, true));
				}
			}
		}
		
		public static void addIngredient(CraftData rec, string item, uint amt) {
			CraftCost cost = new CraftCost();
			cost.Amount = amt;
			cost.Key = item;
			rec.Costs.Add(cost);
			log("Added "+amt+" of "+item+" to recipe "+recipeToString(rec, true));
			link(rec);
		}
		
		public static CraftData addRecipe(string id, string item, int amt = 1, string cat = "Manufacturer") {
			CraftData rec = new CraftData();
			rec.Category = cat;
			rec.Key = "ReikaKalseki."+id;
			rec.CraftedKey = item;
			rec.CraftedAmount = amt;
			CraftData.mRecipesForSet[cat].Add(rec);
			link(rec);
			log("Added new recipe "+recipeToString(rec, true, true));
			return rec;
		}
		
		private static void link(CraftData rec) {
			CraftData.LinkEntries(new List<CraftData>(new CraftData[]{rec}), rec.Category);
		}
		
		public static string ingredientToString(CraftCost ing) {
			return ing.Key+" x "+ing.Amount+" ("+ing.Name+")";
		}
		
		public static string recipeToString(CraftData rec, bool fullIngredients = false, bool fullResearch = false) {
			string ret = "'"+rec.Category+"::"+rec.Key+"'="+rec.CraftedKey+"x"+rec.CraftedAmount+" from ";
			if (fullIngredients) {
				List<string> li = new List<string>();
				rec.Costs.ForEach(c => li.Add(ingredientToString(c)));
				ret += "I["+string.Join(", ", li.ToArray())+"]";
			}
			else {
				ret += rec.Costs.Count+" items";
			}
			ret += " & ";
			if (fullResearch) {
				ret += "T["+string.Join(", ", rec.ResearchRequirements.ToArray())+"]";
			}
			else {
				ret += rec.ResearchRequirements.Count+" techs";
			}
			return ret;
		}
	}
}
