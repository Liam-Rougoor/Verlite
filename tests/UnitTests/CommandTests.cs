using Xunit;

using System;
using System.Runtime.InteropServices;
using System.Text;
using Verlite;
using FluentAssertions;

namespace UnitTests
{
	// if we're on windows, we can also test the operating system agrees
	internal static class Win32
	{
		public static string[] SplitArgs(string unsplitArgumentLine)
		{
			IntPtr ptrToSplitArgs = CommandLineToArgvW(unsplitArgumentLine, out int numberOfArgs);
			if (ptrToSplitArgs == IntPtr.Zero)
				throw new ArgumentException("Unable to split argument.");

			try
			{
				string[] splitArgs = new string[numberOfArgs];
				for (int i = 0; i < numberOfArgs; i++)
					splitArgs[i] = Marshal.PtrToStringUni(
						Marshal.ReadIntPtr(ptrToSplitArgs, i * IntPtr.Size))
							?? throw new ArgumentException($"Unable to read split argument {i}");
				return splitArgs;
			}
			finally
			{
				LocalFree(ptrToSplitArgs);
			}
		}

		[DllImport("shell32.dll", SetLastError = true)]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		private static extern IntPtr CommandLineToArgvW(
			[MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
			out int pNumArgs);

		[DllImport("kernel32.dll")]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		private static extern IntPtr LocalFree(IntPtr hMem);
	}

	public static class EscapeArgumentTests
	{
		private static string EscapeArguments(params string[] args)
		{
			var sb = new StringBuilder();
			bool first = true;

			foreach (var arg in args)
			{
				if (!first)
					sb.Append(' ');
				else
					first = false;

				sb.Append(Command.EscapeArgument(arg));
			}

			return sb.ToString();
		}

		[Fact]
		public static void TestEscapeArgs()
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return;
		
			string[] originalArgs = new string[]
			{
				"hello", // plain argument
				"how\"are",
				"\"you\"",
				"because\\",
				"I'm",
				"\\really good\""
			};

			string escaped = EscapeArguments(originalArgs);
			var split = Win32.SplitArgs(escaped);
		
			split.Should().BeEquivalentTo(originalArgs);
		}

		// some test cases sourced from http://csharptest.net/529/how-to-correctly-escape-command-line-arguments-in-c/index.html
		[Theory]
		[InlineData("a", "a")]
		[InlineData("", "\"\"")]
		[InlineData("\"", "\"\\\"\"")]
		[InlineData("\"\"", "\"\\\"\\\"\"")]
		[InlineData("\"a\"", "\"\\\"a\\\"\"")]
		[InlineData("\\", "\\")]
		[InlineData("a b", "\"a b\"")]
		[InlineData(" a\\b ", "\" a\\b \"")]
		[InlineData(" b", "\" b\"")]
		[InlineData("a\\\\b", "a\\\\b")]
		[InlineData("a\\\\b c", "\"a\\\\b c\"")]
		[InlineData(" \\", "\" \\\\\"")]
		[InlineData(" \\\"", "\" \\\\\\\"\"")]
		[InlineData(" \\\\", "\" \\\\\\\\\"")]
		[InlineData("C:\\Program Files\\", "\"C:\\Program Files\\\\\"")]
		[InlineData("test\"\"\"a", "\"test\\\"\\\"\\\"a\"")]
		[InlineData("\\a\\", "\\a\\")]
		[InlineData("\\a b\\", "\"\\a b\\\\\"")]
		[InlineData("some\rCR", "\"some\rCR\"")]
		[InlineData("some\nLF", "\"some\nLF\"")]

		public static void ArgEscapedCorrectly(string arg, string expected)
		{
			string escaped = Command.EscapeArgument(arg);

			escaped.Should().Be(expected);

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string[] interpretedMid = Win32.SplitArgs($"a.exe {escaped}");
				interpretedMid.Should().BeEquivalentTo(new string[] { "a.exe", arg });
			}
		}

		[Theory]
		[InlineData("some\0null")]
		public static void InvalidCharsWithEscapeArgThrows(string arg)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => Command.EscapeArgument(arg));
		}
	}
}
