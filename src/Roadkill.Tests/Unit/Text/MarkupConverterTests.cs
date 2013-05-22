﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roadkill.Core;
using Roadkill.Core.Configuration;
using Roadkill.Core.Converters;
using Roadkill.Core.Database;

namespace Roadkill.Tests.Unit
{
	[TestFixture]
	[Category("Unit")]
	public class MarkupConverterTests
	{
		private ApplicationSettings _settings;
		private MarkupConverter _converter;
		private RepositoryMock _repository;

		[SetUp]
		public void Setup()
		{
			_settings = new ApplicationSettings();
			_settings.Installed = true;
			_settings.UseHtmlWhiteList = true;
			_settings.CustomTokensPath = Path.Combine(Settings.SITE_PATH, "App_Data", "Tokens.xml");

			_repository = new RepositoryMock();
			_repository.SiteSettings = new SiteSettings();
			_repository.SiteSettings.MarkupType = "Creole";

			_converter = new MarkupConverter(_settings, _repository);
			_converter.AbsolutePathConverter = (path) => { return path; };
			_converter.InternalUrlForTitle = (id, title) => { return title; };
			_converter.NewPageUrlForTitle = (title) => { return title; };
		}

		[Test]
		public void Parser_Should_Not_Be_Null_For_MarkupTypes()
		{
			// Arrange, act
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);

			// Assert
			Assert.NotNull(_converter.Parser);

			_repository.SiteSettings.MarkupType = "Markdown";
			_converter = new MarkupConverter(_settings, _repository);
			Assert.NotNull(_converter.Parser);

			_repository.SiteSettings.MarkupType = "Mediawiki";
			_converter = new MarkupConverter(_settings, _repository);
			Assert.NotNull(_converter.Parser);
		}

		[Test]
		public void ImageParsed_Should_Convert_To_Absolute_Path()
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Markdown";
			_converter = new MarkupConverter(_settings, _repository);

			_converter.AbsolutePathConverter = (string path) => 
			{ 
				return path + "123"; 
			};
			_converter.InternalUrlForTitle = (int pageId, string path) => { return path; };
			_converter.NewPageUrlForTitle = (string path) => { return path; };

			// Act
			bool wasCalled = false;
			_converter.Parser.ImageParsed += (object sender, ImageEventArgs e) =>
			{
				wasCalled = (e.Src == "/Attachments/DSC001.jpg123");
			};

			_converter.ToHtml("![Image title](/DSC001.jpg)");
			
			// Assert
			Assert.True(wasCalled, "ImageParsed.ImageEventArgs.Src did not match.");
		}

		[Test]
		[TestCase("http://i223.photobucket.com/albums/dd45/wally2603/91e7840f.jpg")]
		[TestCase("https://i223.photobucket.com/albums/dd45/wally2603/91e7840f.jpg")]
		[TestCase("www.photobucket.com/albums/dd45/wally2603/91e7840f.jpg")]
		public void ImageParsed_Should_Not_Rewrite_Images_As_Internal_That_Start_With_Known_Prefixes(string imageUrl)
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Markdown";
			_converter = new MarkupConverter(_settings, _repository);

			_converter.AbsolutePathConverter = (string path) =>
			{
				return path + "123";
			};
			_converter.InternalUrlForTitle = (int pageId, string path) => { return path; };
			_converter.NewPageUrlForTitle = (string path) => { return path; };

			bool wasCalled = false;
			_converter.Parser.ImageParsed += (object sender, ImageEventArgs e) =>
			{
				wasCalled = (e.Src == imageUrl);
			};

			// Act
			_converter.ToHtml("![Image title](" +imageUrl+ ")");

			// Assert
			Assert.True(wasCalled);
		}

		[Test]
		public void Should_Remove_Script_Link_Iframe_Frameset_Frame_Applet_Tags_From_Text()
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);
			string markdown = " some text <script type=\"text/html\">while(true)alert('lolz');</script>" +
				"<iframe src=\"google.com\"></iframe><frame>blah</frame> <applet code=\"MyApplet.class\" width=100 height=140></applet>" +
				"<frameset src='new.html'></frameset>";

			string expectedHtml = "<p> some text blah \n</p>";

			// Act
			string actualHtml = _converter.ToHtml(markdown);

			// Assert
			Assert.That(actualHtml, Is.EqualTo(expectedHtml));
		}

		[Test]
		public void Links_Starting_With_Https_Or_Hash_Are_Not_Rewritten_As_Internal()
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);

			string expectedHtml = "<p><a href=\"&#x23;myanchortag\">hello world</a> <a href=\"https&#x3A;&#x2F;&#x2F;www&#x2E;google&#x2E;com\">google</a>\n</p>";

			// Act
			string actualHtml = _converter.ToHtml("[[#myanchortag|hello world]] [[https://www.google.com|google]]");

			// Assert
			Assert.That(actualHtml, Is.EqualTo(expectedHtml));
		}

		[Test]
		public void Links_Starting_With_Tilde_Should_Resolve_As_Attachment_Paths()
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);

			string expectedHtml = "<p><a href=\"&#x2F;Attachments&#x2F;my&#x2F;folder&#x2F;image1&#x2E;jpg\">hello world</a>\n</p>";

			// Act
			string actualHtml = _converter.ToHtml("[[~/my/folder/image1.jpg|hello world]]");

			// Assert
			Assert.That(actualHtml, Is.EqualTo(expectedHtml), actualHtml);
		}

		[Test]
		public void Links_Starting_With_AttachmentColon_Should_Resolve_As_Attachment_Paths()
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);

			string expectedHtml = "<p><a href=\"&#x2F;Attachments&#x2F;my&#x2F;folder&#x2F;image1&#x2E;jpg\">hello world</a>\n</p>";

			// Act
			string actualHtml = _converter.ToHtml("[[attachment:/my/folder/image1.jpg|hello world]]");

			// Assert
			Assert.That(actualHtml, Is.EqualTo(expectedHtml), actualHtml);
		}

		[Test]
		public void Links_Starting_With_Http_Www_Mailto_Tag_Are_No_Rewritten_As_Internal()
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);

			string expectedHtml = "<p><a href=\"http&#x3A;&#x2F;&#x2F;www&#x2E;blah&#x2E;com\">link1</a> <a href=\"www&#x2E;blah&#x2E;com\">link2</a> <a href=\"mailto&#x3A;spam&#x40;gmail&#x2E;com\">spam</a>\n</p>";

			// Act
			string actualHtml = _converter.ToHtml("[[http://www.blah.com|link1]] [[www.blah.com|link2]] [[mailto:spam@gmail.com|spam]]");

			// Assert
			Assert.That(actualHtml, Is.EqualTo(expectedHtml));
		}

		[Test]
		public void Html_Should_Not_Be_Sanitized_If_UseHtmlWhiteList_Setting_Is_False()
		{
			// Arrange
			_settings.UseHtmlWhiteList = false;
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);

			string htmlFragment = "<div onclick=\"javascript:alert('ouch');\">test</div>";
			MarkupConverter converter = new MarkupConverter(_settings, _repository);

			// Act
			string actualHtml = converter.ToHtml(htmlFragment);

			// Assert
			string expectedHtml = "<p>" +htmlFragment+ "\n</p>";
			Assert.That(actualHtml, Is.EqualTo(expectedHtml));
		}

		[Test]
		public void Should_Not_Render_ToC_With_Multiple_Curlies()
		{
			// Arrange
			_repository.SiteSettings.MarkupType = "Creole";
			_converter = new MarkupConverter(_settings, _repository);
			_converter.AbsolutePathConverter = (string s) => { return s; };
			string htmlFragment = "Give me a {{TOC}} and a {{{TOC}}} - the should not render a TOC";
			string expected = @"<p>Give me a <div class=""floatnone""><div class=""image&#x5F;frame""><img src=""&#x2F;Attachments&#x2F;TOC""></div></div> and a TOC - the should not render a TOC"
				+"\n</p>";

			// Act
			string actualHtml = _converter.ToHtml(htmlFragment);

			// Assert
			Assert.That(actualHtml, Is.EqualTo(expected));
		}

		[Test]
		public void Custom_Token_With_NoWiki_Adds_Pre_And_Renders_Token_HTML()
		{
			// Arrange
			string expectedHtml = @"<p><div class=""alert"">ENTER YOUR CONTENT HERE 
<pre>here is my C#code
</pre>
</p>
<p></div><br style=""clear:both""/>
</p>";

			// Act
			string actualHtml = _converter.ToHtml(@"@@warningbox:ENTER YOUR CONTENT HERE 
{{{
here is my C#code
}}} 

@@");

			// Assert
			Assert.That(actualHtml, Is.EqualTo(expectedHtml), actualHtml);
		}

		// TODO:
		// ContainsPageLink
		// ReplacePageLinks
		// TOCParser
		// Creole tests
	}
}
