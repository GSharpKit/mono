//
// System.Diagnostics.SymbolStore/MonoSymbolWriter.cs
//
// Author:
//   Martin Baulig (martin@gnome.org)
//
// This is the default implementation of the System.Diagnostics.SymbolStore.ISymbolWriter
// interface.
//
// (C) 2002 Ximian, Inc.  http://www.ximian.com
//

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics.SymbolStore;
using System.Collections;
using System.IO;
	
namespace Mono.CSharp.Debugger
{

	public class MonoSymbolWriter : IMonoSymbolWriter
	{
		protected string output_filename = null;
		protected Hashtable methods = null;
		protected Hashtable sources = null;

		protected class SourceFile : ISourceFile
		{
			private ISourceMethod[] _methods;
			private string _file_name;

			public SourceFile (string filename)
			{
				this._file_name = filename;

				this._methods = new ISourceMethod [0];
			}

			// interface ISourceFile

			public string FileName {
				get {
					return _file_name;
				}
			}

			public ISourceMethod[] Methods {
				get {
					return _methods;
				}
			}

			public void AddMethod (ISourceMethod method)
			{
				int i = _methods.Length;
				ISourceMethod[] new_m = new ISourceMethod [i + 1];
				Array.Copy (_methods, new_m, i);
				new_m [i] = method;
				_methods = new_m;
			}
		}

		protected class SourceLine : ISourceLine
		{
			public SourceLine (int offset, int line)
			{
				this._offset = offset;
				this._line = line;
			}

			private readonly int _offset;
			private readonly int _line;

			// interface ISourceLine

			public int Offset {
				get {
					return _offset;
				}
			}

			public int Line {
				get {
					return _line;
				}
			}
		}

		protected class LocalVariable : ILocalVariable
		{
			public LocalVariable (string name, int index)
			{
				this._name = name;
				this._index = index;
			}

			private readonly string _name;
			private readonly int _index;

			// interface ILocalVariable

			public string Name {
				get {
					return _name;
				}
			}

			public int Index {
				get {
					return _index;
				}
			}
		}

		protected class SourceMethod : ISourceMethod
		{
			private ISourceLine[] _lines;
			private ILocalVariable[] _locals;

			private readonly MethodInfo _method_info;
			private readonly SourceFile _source_file;

			public SourceMethod (MethodInfo method_info, SourceFile source_file) {
				this._method_info = method_info;
				this._source_file = source_file;

				this._lines = new ISourceLine [0];
				this._locals = new ILocalVariable [0];
			}

			public void SetSourceRange (int startLine, int startColumn,
						    int endLine, int endColumn)
			{
				AddLine (new SourceLine (0, startLine));
			}

			// interface ISourceMethod

			public ISourceLine[] Lines {
				get {
					return _lines;
				}
			}

			public ILocalVariable[] Locals {
				get {
					return _locals;
				}
			}

			public void AddLine (ISourceLine line)
			{
				int i = _lines.Length;
				ISourceLine[] new_m = new ISourceLine [i + 1];
				Array.Copy (_lines, new_m, i);
				new_m [i] = line;
				_lines = new_m;
			}

			public void AddLocal (ILocalVariable local)
			{
				int i = _locals.Length;
				ILocalVariable[] new_m = new ILocalVariable [i + 1];
				Array.Copy (_locals, new_m, i);
				new_m [i] = local;
				_locals = new_m;
			}

			public MethodInfo MethodInfo {
				get {
					return _method_info;
				}
			}

			public ISourceFile SourceFile {
				get {
					return _source_file;
				}
			}
		}

		protected SourceMethod current_method = null;

		//
		// Interface IMonoSymbolWriter
		//

		public MonoSymbolWriter ()
		{
			methods = new Hashtable ();
			sources = new Hashtable ();
		}

		public void Close () {
			CreateDwarfFile (output_filename);
		}

		public void CloseNamespace () {
		}

		public void CloseScope (int endOffset) {
		}

		// Create and return a new IMonoSymbolDocumentWriter.
		public ISymbolDocumentWriter DefineDocument (string url,
							     Guid language,
							     Guid languageVendor,
							     Guid documentType)
		{
			return new MonoSymbolDocumentWriter (url);
		}

		public void DefineField (
			SymbolToken parent,
			string name,
			FieldAttributes attributes,
			byte[] signature,
			SymAddressKind addrKind,
			int addr1,
			int addr2,
			int addr3)
		{
		}

		public void DefineGlobalVariable (
			string name,
			FieldAttributes attributes,
			byte[] signature,
			SymAddressKind addrKind,
			int addr1,
			int addr2,
			int addr3)
		{
		}

		public void DefineLocalVariable (string name,
						 FieldAttributes attributes,
						 byte[] signature,
						 SymAddressKind addrKind,
						 int addr1,
						 int addr2,
						 int addr3,
						 int startOffset,
						 int endOffset)
		{
			LocalVariable local_info = new LocalVariable (name, addr1);

			if (current_method != null)
				current_method.AddLocal (local_info);
		}

		public void DefineParameter (
			string name,
			ParameterAttributes attributes,
			int sequence,
			SymAddressKind addrKind,
			int addr1,
			int addr2,
			int addr3)
		{
		}

		public void DefineSequencePoints (ISymbolDocumentWriter document,
						  int[] offsets,
						  int[] lines,
						  int[] columns,
						  int[] endLines,
						  int[] endColumns)
		{
			SourceLine source_line = new SourceLine (offsets [0], lines [0]);

			if (current_method != null)
				current_method.AddLine (source_line);
		}

		public void Initialize (IntPtr emitter, string filename, bool fFullBuild)
		{
			throw new NotSupportedException ("Please use the 'Initialize (string filename)' "
							 + "constructor and read the documentation in "
							 + "Mono.CSharp.Debugger/IMonoSymbolWriter.cs");
		}

		// This is documented in IMonoSymbolWriter.cs
		public void Initialize (string filename)
		{
			this.output_filename = filename;
		}

		public void OpenMethod (SymbolToken method)
		{
			// do nothing
		}

		// This is documented in IMonoSymbolWriter.cs
		public void OpenMethod (SymbolToken symbol_token, MethodInfo method_info,
					string source_file)
		{
			int token = symbol_token.GetToken ();
			SourceFile source_info;

			if (methods.ContainsKey (token))
				methods.Remove (token);

			if (sources.ContainsKey (source_file))
				source_info = (SourceFile) sources [source_file];
			else {
				source_info = new SourceFile (source_file);
				sources.Add (source_file, source_info);
			}

			current_method = new SourceMethod (method_info, source_info);

			source_info.AddMethod (current_method);

			methods.Add (token, current_method);

			OpenMethod (symbol_token);
		}

		public void SetMethodSourceRange (ISymbolDocumentWriter startDoc,
						  int startLine, int startColumn,
						  ISymbolDocumentWriter endDoc,
						  int endLine, int endColumn)
		{
			if ((startDoc == null) || (endDoc == null))
				throw new NullReferenceException ();

			if (!(startDoc is MonoSymbolDocumentWriter) || !(endDoc is MonoSymbolDocumentWriter))
				throw new NotSupportedException ("both startDoc and endDoc must be of type "
								 + "MonoSymbolDocumentWriter");

			if (!startDoc.Equals (endDoc))
				throw new NotSupportedException ("startDoc and endDoc must be the same");

			if (current_method != null)
				current_method.SetSourceRange (startLine, startColumn,
							       endLine, endColumn);

			Console.WriteLine ("SOURCE RANGE");
		}

		public void CloseMethod () {
			current_method = null;
		}

		public void OpenNamespace (string name)
		{
		}

		public int OpenScope (int startOffset)
		{
			throw new NotImplementedException ();
		}

		public void SetScopeRange (int scopeID, int startOffset, int endOffset)
		{
		}

		public void SetSymAttribute (SymbolToken parent, string name, byte[] data)
		{
		}

		public void SetUnderlyingWriter (IntPtr underlyingWriter)
		{
		}

		public void SetUserEntryPoint (SymbolToken entryMethod)
		{
		}

		public void UsingNamespace (string fullName)
		{
		}

		//
		// MonoSymbolWriter implementation
		//
		protected void WriteMethod (DwarfFileWriter.DieCompileUnit parent_die, ISourceMethod method)
		{
			Console.WriteLine ("WRITING METHOD: " + method.MethodInfo.Name);

			DwarfFileWriter.DieSubProgram die;

			die = new DwarfFileWriter.DieSubProgram (parent_die, method);
		}

		protected void WriteSource (DwarfFileWriter writer, ISourceFile source)
		{
			Console.WriteLine ("WRITING SOURCE: " + source.FileName);

			DwarfFileWriter.CompileUnit compile_unit = new DwarfFileWriter.CompileUnit (
				writer, source.FileName);

			DwarfFileWriter.DieCompileUnit die = new DwarfFileWriter.DieCompileUnit (compile_unit);

			foreach (ISourceMethod method in source.Methods)
				WriteMethod (die, method);
		}

		protected void CreateDwarfFile (string filename)
		{
			Console.WriteLine ("WRITING DWARF FILE: " + filename);

			DwarfFileWriter writer = new DwarfFileWriter (filename);

			foreach (ISourceFile source in sources.Values)
				WriteSource (writer, source);

			writer.Close ();

			Console.WriteLine ("DONE WRITING DWARF FILE");

		}
	}
}
