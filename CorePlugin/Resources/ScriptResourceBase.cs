﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Duality;
using Duality.Editor;

namespace ScriptingPlugin.Resources
{
	[Serializable]
	public abstract class ScriptResourceBase : Resource
	{
		public string Script { get; set; }
		
		[EditorHintFlags(MemberFlags.Invisible)]
		public Assembly Assembly
		{
			get { return _assembly; }
		}

		[field: NonSerialized]
		public event EventHandler Reloaded;

		[NonSerialized]
		private Assembly _assembly;
		
		[NonSerialized]
		protected IScriptCompilerService ScriptCompiler;

		public void SaveScript(string scriptPath)
		{
			if (scriptPath == null)
				scriptPath = sourcePath;

			if (!IsDefaultContent && sourcePath == null)
				sourcePath = scriptPath;

			File.WriteAllText(sourcePath, Script);
		}

		protected override void OnLoaded()
		{
			Compile();
			base.OnLoaded();
		}

		private ScriptsResult Compile()
		{
			const string scriptsDll = "Scripts\\Scripts.dll";
			if (File.Exists(scriptsDll))
			{
				_assembly = Assembly.LoadFile(System.IO.Path.GetFullPath(scriptsDll));
				return ScriptsResult.AssemblyExists;
			}
			try
			{
				if (!string.IsNullOrEmpty(SourcePath))
					return ScriptCompiler.TryCompile(Name, SourcePath, Script, out _assembly);

			}
			catch (Exception e)
			{
				Log.Editor.WriteError("Error trying to compile script {0}.Message {1} \n {2}", Name, e.Message, e.StackTrace);	
			}
			Log.Editor.WriteWarning("The script resource '{0}' has no SourcePath and can't be compiled.", Name);	
			
			return ScriptsResult.GeneralError;
		}

		public DualityScript Instantiate()
		{
			if (Assembly == null)
			{
				var compiled = Compile();

				if (Assembly == null || compiled != ScriptsResult.AssemblyExists)
				{
					Log.Editor.WriteWarning("Couldn't compile script '{0}'", Name);
					return null;
				}
			}

			var script = Assembly.GetTypes().FirstOrDefault(t => t.BaseType != null && t.BaseType == typeof(DualityScript) && t.Name == Name);

			if (script == null)
			{
				Log.Game.WriteError("Could not create an instance of script '{0}' because it does not contain a type derived from DualityScript.", Name);
				return null;
			}

			return (DualityScript)Activator.CreateInstance(script);
		}

		public void Reload()
		{
			Compile();

			var metafilePath = System.IO.Path.GetFullPath(GetMetafilePath());

			if (File.Exists(metafilePath))
			{
				var fileInfo = new FileInfo(metafilePath);
				fileInfo.Attributes &= ~FileAttributes.Hidden;
			}

			File.WriteAllText(metafilePath, "");
			File.SetLastWriteTime(metafilePath, DateTime.Now);
			File.SetAttributes(metafilePath, FileAttributes.Hidden);

			OnReloaded();
		}

		public string GetMetafilePath()
		{
			return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetFileNameWithoutExtension(Path) + ".meta");
		}

		protected virtual void OnReloaded()
		{
			var handler = Reloaded;
			if (handler != null)
				handler(this, EventArgs.Empty);
		}

	}
}