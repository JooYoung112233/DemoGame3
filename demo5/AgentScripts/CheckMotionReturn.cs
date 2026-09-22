using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Demo5.FrontEnd;
public static class CheckMotionReturn {
 public static async Task<string> Run(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();var m=UnityEngine.Object.FindAnyObjectByType<SettlementPawnMotion>();var p=c.CraftPanel;if(!p.IsOpen)p.Open();await Task.Delay(300);p.OrderRows[0].Cancel.onClick.Invoke();p.CancelYes.onClick.Invoke();p.Close();await Task.Delay(3500);if(m.IsMoving||m.WorkBadges[0].gameObject.activeSelf||Vector3.Distance(m.Pawns[0].position,Vector3.zero)>.01f)throw new Exception("Return failed");return "PASS: cancellation restored original pawn position and hid badge.";}
}
