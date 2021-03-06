﻿using SixLabors.Fonts.Tests.Fakes;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.Fonts.Tests.Issues
{
    public class Issues_32
    {
        [Fact]
        public void TabWidth0CausesBadTabRendering()
        {
            var text = "Hello\tworld";
            var font  = CreateFont(text);
            SizeF size = TextMeasurer.MeasureBounds(text, new RendererOptions(font, (72 * font.EmSize))
            {
                TabWidth = 0
            }).Size;

            // tab width of 0 should make tabs not render at all
            Assert.Equal(10, size.Height, 4);
            Assert.Equal(280, size.Width, 4);
        }

        public static Font CreateFont(string text)
        {
            FontCollection fc = new FontCollection();
            Font d = fc.Install(new FakeFontInstance(text)).CreateFont(12);
            return new Font(d, 1);
        }
    }
}
