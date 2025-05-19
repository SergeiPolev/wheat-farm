#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public class EnumGenerator
{
    [MenuItem( "Tools/GenerateEnum" )]
    public static void Go()
    {
        string enumName = "SkillTypeID";
        string enumStart = "Skill_";
        int amount = 108;
        string filePathAndName = "Assets/Scripts/Skills/" + enumName + ".cs"; //The folder Scripts/Skills/ is expected to exist

        using ( StreamWriter streamWriter = new StreamWriter( filePathAndName ) )
        {
            streamWriter.WriteLine( "public enum " + enumName );
            streamWriter.WriteLine( "{" );
            
            for( int i = 1; i <= amount ; i++ )
            {
                streamWriter.WriteLine( $"	{enumStart}{i:00},");
            }
            
            streamWriter.WriteLine( "}" );
        }
        
        AssetDatabase.Refresh();
    }
}
#endif