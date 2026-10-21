namespace Staredown
{
 public static class StareDanceCatalog
 {
  public const int Count=8;
  static readonly string[] names={"Hip hop","Charleston","Lindy hop","Fancy footwork","Swing step","Kick step","Rhythm wave","Victory groove"};
  static readonly int[] display={0,6,7,3,1,2,4,5};
  public static string Name(int id)=>id>=0&&id<Count?names[id]:"Stop dancing";
  public static string State(int id)=>id>0&&id<Count?"Dance"+id:"Dance";
  public static int DisplayId(int slot)=>display[slot];
  public static string Group(int group)=>group==0?"GROOVES":"SWING";
 }
}
