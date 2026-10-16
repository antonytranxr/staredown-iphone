using System;
using System.Text;
using UnityEngine;

namespace Staredown
{
 public static class StarePlayerName
 {
  public const int MaxLength=16;
  const string Preference="Staredown.PlayerName";
  public static bool InputCharacter(char value)=>char.IsLetterOrDigit(value)||char.IsWhiteSpace(value)||value=='-'||value=='_'||value=='\'';
  public static string Clean(string value)
  {
   if(string.IsNullOrEmpty(value))return "";
   try{value=value.Normalize(NormalizationForm.FormC);}catch(ArgumentException){/* Invalid surrogate characters are filtered below. */}
   var result=new StringBuilder(MaxLength);bool space=false;
   foreach(char character in value)
   {
    if(char.IsWhiteSpace(character)){space=result.Length>0;continue;}
    if(!InputCharacter(character))continue;
    if(space&&result.Length<MaxLength){result.Append(' ');space=false;}
    if(result.Length>=MaxLength)break;
    result.Append(character);
   }
   return result.ToString().Trim();
  }
  public static string Saved
  {get{string value=Clean(PlayerPrefs.GetString(Preference,"Player"));return value.Length>0?value:"Player";}}
  public static string Save(string value)
  {
   value=Clean(value);if(value.Length==0)value="Player";
   PlayerPrefs.SetString(Preference,value);PlayerPrefs.Save();return value;
  }
#if UNITY_EDITOR
  public static string Verify()
  {
   int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Player name: "+label);count++;};
   check(Clean(null)==""&&Clean("   ")=="","empty input does not become an authoritative name");
   check(Clean("  Antony\n\t VR  ")=="Antony VR","whitespace normalizes to one line");
   check(Clean("Ava<>\u0000\u202e🕶️")=="Ava","markup, controls, bidi overrides and emoji cannot enter labels");
   check(Clean("O'Neil-7_VR")=="O'Neil-7_VR","common name punctuation is preserved");
   check(Clean("Jose\u0301")=="José","composed accented name survives");
   check(Clean(new string('X',100)).Length==MaxLength,"long names are bounded");
   check(Clean("A\ud800 B")=="A B","invalid surrogate is ignored safely");
   return count+" player-name sanitization checks passed";
  }
#endif
 }
}
