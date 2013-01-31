using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
namespace QuickFont
{
    public class QFont : IDisposable
    {
        private IFontRenderer renderer;
        
        //private QFontRenderOptions options = new QFontRenderOptions();
        private Stack<QFontRenderOptions> optionsStack = new Stack<QFontRenderOptions>();
        private QFontData fontData;

        private FontLoadDescription fontLoadDescription;


        public QFontRenderOptions Options
        {
            get {

                if (optionsStack.Count == 0)
                {
                    optionsStack.Push(new QFontRenderOptions());
                }

                return optionsStack.Peek() ; 
            }
            private set { //not sure if we should even allow this...
                optionsStack.Pop();
                optionsStack.Push(value);
            }
        }


        #region Constructors and font builders

        /// <summary>
        /// Reloads the font using the original loader/builder options. This may be useful if the underlying
        /// files change, or, more commonly, if the "TransformToCurrentOrthogProjection" option was used when
        /// creating the font, and the orthog projection has since changed (e.g. resizing the window). This 
        /// will do nothing for fonts created directly from a Font object.
        /// </summary>
        public void Reload()
        {
            switch (fontLoadDescription.Method)
            {
                case FontLoadMethod.QFontFile:
                    {
                        fontData.Dispose(); //dispose old data
                        LoadQFontFromQFontFile(fontLoadDescription);
                        break;
                    }

                case FontLoadMethod.FontFile:
                    {
                        fontData.Dispose(); //dispose old data
                        LoadQFontFromFontFile(fontLoadDescription);
                        break;
                    }
            }
        }

        private void LoadTextures(QFontData fdata)
        {
            if (fdata.dropShadow != null) LoadTextures(fdata.dropShadow);
            foreach (var page in fdata.Pages)
            {
                page.Texture = renderer.CreateTexture(page.BitmapData);
                page.Free();
            }
        }


        private QFont(IFontRenderer renderer) { this.renderer = renderer; }
        internal QFont(IFontRenderer renderer, QFontData fontData): this(renderer) { this.fontData = fontData; }
        public QFont(IFontRenderer renderer, Font font, QFontBuilderConfiguration config = null): this(renderer)
        {
            if (config == null)
                config = new QFontBuilderConfiguration();

            fontData = BuildFont(font, config, null);
            LoadTextures(fontData);

            if (config.ShadowConfig != null)
                Options.DropShadowActive = true;
        }
        

        private void LoadQFontFromFontFile(FontLoadDescription loadDescription)
        {
            var config = loadDescription.BuilderConfig;
            var fileName = loadDescription.Path;
            var size = loadDescription.Size;
            var style = loadDescription.Style;

            if (config == null)
                config = new QFontBuilderConfiguration();
            
            //dont move this into a separate method - it needs to stay in scope!
            PrivateFontCollection pfc = new PrivateFontCollection();
            pfc.AddFontFile(fileName);
            var fontFamily = pfc.Families[0];

            if (!fontFamily.IsStyleAvailable(style))
                throw new ArgumentException("Font file: " + fileName + " does not support style: " + style);


            var font = new Font(fontFamily, size * config.SuperSampleLevels, style);
            //var font = ObtainFont(fileName, size * fontScale * config.SuperSampleLevels, style)
            fontData = BuildFont(font, config, null);
            LoadTextures(fontData);
            fontData.scaleDueToTransformToViewport = 1f;
            font.Dispose();

            if (config.ShadowConfig != null)
                Options.DropShadowActive = true;
        }


        private void LoadQFontFromQFontFile(FontLoadDescription loadDescription)
        {
            var loaderConfig = loadDescription.LoaderConfig;
            var filePath = loadDescription.Path;
            var downSampleFactor = loadDescription.DownSampleFactor;


            if (loaderConfig == null)
                loaderConfig = new QFontLoaderConfiguration();

            fontData = Builder.LoadQFontDataFromFile(filePath, downSampleFactor, loaderConfig);
            LoadTextures(fontData);
            fontData.scaleDueToTransformToViewport = 1f;

            if (loaderConfig.ShadowConfig != null)
                Options.DropShadowActive = true;
        }

        public QFont(IFontRenderer renderer, string fileName, float size, FontStyle style = FontStyle.Regular, QFontBuilderConfiguration config = null)
        {
            this.renderer = renderer;
            fontLoadDescription = new FontLoadDescription(fileName, size, style, config);
            LoadQFontFromFontFile(fontLoadDescription);
        }

        public static void CreateTextureFontFiles(Font font, string newFontName, QFontBuilderConfiguration config = null)
        {
            var fontData = BuildFont(font, config, newFontName);
            Builder.SaveQFontDataToFile(fontData, newFontName);
        }
        
        public static void CreateTextureFontFiles(string fileName, float size, string newFontName, FontStyle style = FontStyle.Regular, QFontBuilderConfiguration config = null)
        {

            QFontData fontData;
            if (config == null)
                config = new QFontBuilderConfiguration();


            //dont move this into a separate method - it needs to stay in scope!
            PrivateFontCollection pfc = new PrivateFontCollection();
            pfc.AddFontFile(fileName);
            var fontFamily = pfc.Families[0];

            if (!fontFamily.IsStyleAvailable(style))
                throw new ArgumentException("Font file: " + fileName + " does not support style: " + style);

            var font = new Font(fontFamily, size * config.SuperSampleLevels, style);
            //var font = ObtainFont(fileName, size * config.SuperSampleLevels, style);
            try
            {
                fontData = BuildFont(font, config, newFontName);
            }
            finally
            {
                if (font != null)
                    font.Dispose();
            }

            Builder.SaveQFontDataToFile(fontData, newFontName);
            
        }

        public static QFont FromQFontFile(IFontRenderer renderer, string filePath, float downSampleFactor = 1.0f, QFontLoaderConfiguration loaderConfig = null)
        {
            QFont qfont = new QFont(renderer);
            qfont.fontLoadDescription = new FontLoadDescription(filePath,downSampleFactor,loaderConfig);
            qfont.LoadQFontFromQFontFile(qfont.fontLoadDescription);
            return qfont;
        }
  
        private static QFontData BuildFont(Font font, QFontBuilderConfiguration config, string saveName)
        {
            Builder builder = new Builder(font, config);
            return builder.BuildFontData(saveName);
        }



        #endregion
        

        /// <summary>
        /// Pushes the specified QFont options onto the options stack
        /// </summary>
        /// <param name="newOptions"></param>
        public void PushOptions(QFontRenderOptions newOptions)
        {
            optionsStack.Push(newOptions);
        }

        /// <summary>
        /// Creates a clone of the current font options and pushes
        /// it onto the stack
        /// </summary>
        public void PushOptions()
        {
            PushOptions(Options.CreateClone());
        }

        public void PopOptions()
        {
            if (optionsStack.Count > 1)
            {
                optionsStack.Pop();
            }
            else
            {
                throw new Exception("Attempted to pop from options stack when there is only one Options object on the stack.");
            }
        }



        public float LineSpacing
        {
            get { return (float)Math.Ceiling(fontData.maxGlyphHeight * Options.LineSpacing); }
        }

        public bool IsMonospacingActive
        {
            get { return fontData.IsMonospacingActive(Options); }
        }


        public float MonoSpaceWidth
        {
            get { return fontData.GetMonoSpaceWidth(Options); }
        }



        private void RenderDropShadow(float x, float y, char c, QFontGlyph nonShadowGlyph)
        {
            //note can cast drop shadow offset to int, but then you can't move the shadow smoothly...
            if (fontData.dropShadow != null && Options.DropShadowActive)
            {
                var fData = fontData;
                fontData = fontData.dropShadow;
                RenderGlyph(
                    x + (fontData.meanGlyphWidth * Options.DropShadowOffset.X + nonShadowGlyph.rect.Width * 0.5f),
                    y + (fontData.meanGlyphWidth * Options.DropShadowOffset.Y + nonShadowGlyph.rect.Height * 0.5f + nonShadowGlyph.yOffset), c, true);
                fontData = fData;
            }
        }

        public void RenderGlyph(float x, float y, char c, bool isDropShadow)
        {
            var glyph = fontData.CharSetMapping[c];

            //note: it's not immediately obvious, but this combined with the paramteters to 
            //RenderGlyph for the shadow mean that we render the shadow centrally (despite it being a different size)
            //under the glyph
            if (isDropShadow) 
            {
                x -= (int)(glyph.rect.Width * 0.5f);
                y -= (int)(glyph.rect.Height * 0.5f + glyph.yOffset);
            }


            RenderDropShadow(x, y, c, glyph);

            var sheet = fontData.Pages[glyph.page].Texture;

            Quad q = new Quad
                         {
                             Texture = sheet,
                             Color = isDropShadow ? Color.FromArgb((int)(Options.DropShadowOpacity * 255), 255, 255, 255) : Options.Color,
                             U1 = (float) (glyph.rect.X)/sheet.Width,
                             V1 = (float) (glyph.rect.Y)/sheet.Height,
                             U2 = (float) (glyph.rect.X + glyph.rect.Width)/sheet.Width,
                             V2 = (float) (glyph.rect.Y + glyph.rect.Height)/sheet.Height,
                             X1 = x,
                             Y1 = y + glyph.yOffset,
                             X2 = x + glyph.rect.Width,
                             Y2 = y + glyph.yOffset + glyph.rect.Height
                         };
            renderer.DrawQuad(q);
        }


        


        private float MeasureNextlineLength(string text)
        {

            float xOffset = 0;
            
            for(int i=0; i < text.Length;i++)
            {
                char c = text[i];

                if (c == '\r' || c == '\n')
                {
                    break;
                }


                if (IsMonospacingActive)
                {
                    xOffset += MonoSpaceWidth;
                }
                else
                {
                    //space
                    if (c == ' ')
                    {
                        xOffset += (float)Math.Ceiling(fontData.meanGlyphWidth * Options.WordSpacing);
                    }
                    //normal character
                    else if (fontData.CharSetMapping.ContainsKey(c))
                    {
                        QFontGlyph glyph = fontData.CharSetMapping[c];
                        xOffset += (float)Math.Ceiling(glyph.rect.Width + fontData.meanGlyphWidth * Options.CharacterSpacing + fontData.GetKerningPairCorrection(i, text, null));
                    }
                }
            }
            return xOffset;
        }

        private PointF LockToPixel(PointF input)
        {
            if (Options.LockToPixel)
            {
                float r = Options.LockToPixelRatio;
                return new PointF((1 - r) * input.X + r * ((int)Math.Round(input.X)), (1 - r) * input.Y + r * ((int)Math.Round(input.Y)));
            }
            return input;
        }



        public void Print(ProcessedText processedText, PointF position)
        {
            position = LockToPixel(position);

            renderer.Begin(position);
            Print(processedText);
            renderer.End();
        }

        public void Print(string text, float maxWidth, QFontAlignment alignment, PointF position)
        {
            position = LockToPixel(position);

            renderer.Begin(position);
            Print(text, maxWidth, alignment);
            renderer.End();
        }
        
        public void Print(string text, QFontAlignment alignment = QFontAlignment.Left, PointF position = default(PointF))
        {
            position = LockToPixel(position);

            renderer.Begin(position);
            PrintOrMeasure(text, alignment, false);
            renderer.End();
        }


        public SizeF Measure(string text, QFontAlignment alignment = QFontAlignment.Left)
        {
            return PrintOrMeasure(text, alignment, true);
        }

        /// <summary>
        /// Measures the actual width and height of the block of text.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="maxWidth"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        public SizeF Measure(string text, float maxWidth, QFontAlignment alignment)
        {
            var processedText = ProcessText(text, maxWidth, alignment);
            return Measure(processedText);
        }

        /// <summary>
        /// Measures the actual width and height of the block of text
        /// </summary>
        /// <param name="processedText"></param>
        /// <returns></returns>
        public SizeF Measure(ProcessedText processedText)
        {
            return PrintOrMeasure(processedText, true);
        }


        private SizeF PrintOrMeasure(string text, QFontAlignment alignment, bool measureOnly)
        {
            float maxXpos = float.MinValue;
            float minXPos = float.MaxValue;
            
            float xOffset = 0f;
            float yOffset = 0f;

            text = text.Replace("\r\n", "\r");

            if (alignment == QFontAlignment.Right)
                xOffset -= MeasureNextlineLength(text);
            else if (alignment == QFontAlignment.Centre)
                xOffset -= (int)(0.5f * MeasureNextlineLength(text));

            for(int i = 0; i < text.Length; i++)
            {
                char c = text[i];


                //newline
                if (c == '\r' || c == '\n')
                {
                    yOffset += LineSpacing;
                    xOffset = 0f;

                    if (alignment == QFontAlignment.Right)
                        xOffset -= MeasureNextlineLength(text.Substring(i + 1));
                    else if (alignment == QFontAlignment.Centre)
                        xOffset -= (int)(0.5f * MeasureNextlineLength(text.Substring(i + 1)));

                }
                else
                {

                    minXPos = Math.Min(xOffset, minXPos);

                    //normal character
                    if (c != ' ' && fontData.CharSetMapping.ContainsKey(c))
                    {
                        QFontGlyph glyph = fontData.CharSetMapping[c];
                        if(!measureOnly)
                            RenderGlyph(xOffset, yOffset, c, false);
                    }


                    if (IsMonospacingActive)
                        xOffset += MonoSpaceWidth;
                    else
                    {
                        if (c == ' ')
                            xOffset += (float)Math.Ceiling(fontData.meanGlyphWidth * Options.WordSpacing);
                        //normal character
                        else if (fontData.CharSetMapping.ContainsKey(c))
                        {
                            QFontGlyph glyph = fontData.CharSetMapping[c];
                            xOffset += (float)Math.Ceiling(glyph.rect.Width + fontData.meanGlyphWidth * Options.CharacterSpacing + fontData.GetKerningPairCorrection(i, text, null));
                        }
                    }

                    maxXpos = Math.Max(xOffset, maxXpos);
                }

            }

            float maxWidth = 0f;

            if (minXPos != float.MaxValue)
                maxWidth = maxXpos - minXPos;
            
            return new SizeF(maxWidth, yOffset + LineSpacing);
        }








        private void RenderWord(float x, float y, TextNode node)
        {

            if (node.Type != TextNodeType.Word)
                return;

            int charGaps = node.Text.Length - 1;
            bool isCrumbleWord = CrumbledWord(node);
            if (isCrumbleWord)
                charGaps++;

            int pixelsPerGap = 0;
            int leftOverPixels = 0;

            if (charGaps != 0)
            {
                pixelsPerGap = (int)node.LengthTweak / charGaps;
                leftOverPixels = (int)node.LengthTweak - pixelsPerGap * charGaps;
            }

            for(int i = 0; i < node.Text.Length; i++){
                char c = node.Text[i];
                if(fontData.CharSetMapping.ContainsKey(c)){
                    var glyph = fontData.CharSetMapping[c];

                    RenderGlyph(x,y,c, false);


                    if (IsMonospacingActive)
                        x += MonoSpaceWidth;
                    else
                        x += (int)Math.Ceiling(glyph.rect.Width + fontData.meanGlyphWidth * Options.CharacterSpacing + fontData.GetKerningPairCorrection(i, node.Text, node));

                    x += pixelsPerGap;
                    if (leftOverPixels > 0)
                    {
                        x += 1.0f;
                        leftOverPixels--;
                    }
                    else if (leftOverPixels < 0)
                    {
                        x -= 1.0f;
                        leftOverPixels++;
                    }


                }
            }
        }






        /// <summary>
        /// Computes the length of the next line, and whether the line is valid for
        /// justification.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="maxLength"></param>
        /// <param name="justifable"></param>
        /// <returns></returns>
        private float TextNodeLineLength(TextNode node, float maxLength)
        {

            if (node == null)
                return 0;

            bool atLeastOneNodeCosumedOnLine = false;
            float length = 0;
            for (; node != null; node = node.Next)
            {

                if (node.Type == TextNodeType.LineBreak)
                    break;

                if (SkipTrailingSpace(node, length, maxLength) && atLeastOneNodeCosumedOnLine)
                    break;

                if (length + node.Length <= maxLength || !atLeastOneNodeCosumedOnLine)
                {
                    atLeastOneNodeCosumedOnLine = true;
                    length += node.Length;
                }
                else
                {
                    break;
                }


            }
            return length;
        }


        private bool CrumbledWord(TextNode node)
        {
            return (node.Type == TextNodeType.Word && node.Next != null && node.Next.Type == TextNodeType.Word);  
        }


        /// <summary>
        /// Computes the length of the next line, and whether the line is valid for
        /// justification.
        /// </summary>
        private void JustifyLine(TextNode node, float targetLength)
        {
  
            bool justifiable = false;

            if (node == null)
                return;

            var headNode = node; //keep track of the head node


            //start by finding the length of the block of text that we know will actually fit:

            int charGaps = 0;
            int spaceGaps = 0;

            bool atLeastOneNodeCosumedOnLine = false;
            float length = 0;
            var expandEndNode = node; //the node at the end of the smaller list (before adding additional word)
            for (; node != null; node = node.Next)
            {

                

                if (node.Type == TextNodeType.LineBreak)
                    break;

                if (SkipTrailingSpace(node, length, targetLength) && atLeastOneNodeCosumedOnLine)
                {
                    justifiable = true;
                    break;
                }

                if (length + node.Length < targetLength || !atLeastOneNodeCosumedOnLine)
                {

                    expandEndNode = node;

                    if (node.Type == TextNodeType.Space)
                        spaceGaps++;

                    if (node.Type == TextNodeType.Word)
                    {
                        charGaps += (node.Text.Length - 1);

                        //word was part of a crumbled word, so there's an extra char cap between the two words
                        if (CrumbledWord(node))
                            charGaps++;

                    }

                    atLeastOneNodeCosumedOnLine = true;
                    length += node.Length;
                }
                else
                {
                    justifiable = true;
                    break;
                }

                

            }


            //now we check how much additional length is added by adding an additional word to the line
            float extraLength = 0f;
            int extraSpaceGaps = 0;
            int extraCharGaps = 0;
            bool contractPossible = false;
            TextNode contractEndNode = null;
            for (node = expandEndNode.Next; node != null; node = node.Next)
            {
                

                if (node.Type == TextNodeType.LineBreak)
                    break;

                if (node.Type == TextNodeType.Space)
                {
                    extraLength += node.Length;
                    extraSpaceGaps++;
                } 
                else if (node.Type == TextNodeType.Word)
                {
                    contractEndNode = node;
                    contractPossible = true;
                    extraLength += node.Length;
                    extraCharGaps += (node.Text.Length - 1);
                    break;
                }
            }



            if (justifiable)
            {

                //last part of this condition is to ensure that the full contraction is possible (it is all or nothing with contractions, since it looks really bad if we don't manage the full)
                bool contract = contractPossible && (extraLength + length - targetLength) * Options.JustifyContractionPenalty < (targetLength - length) &&
                    ((targetLength - (length + extraLength + 1)) / targetLength > -Options.JustifyCapContract); 

                if((!contract && length < targetLength) || (contract && length + extraLength > targetLength))  //calculate padding pixels per word and char
                {

                    if (contract)
                    {
                        length += extraLength + 1; 
                        charGaps += extraCharGaps;
                        spaceGaps += extraSpaceGaps;
                    }

                    

                    int totalPixels = (int)(targetLength - length); //the total number of pixels that need to be added to line to justify it
                    int spacePixels = 0; //number of pixels to spread out amongst spaces
                    int charPixels = 0; //number of pixels to spread out amongst char gaps





                    if (contract)
                    {

                        if (totalPixels / targetLength < -Options.JustifyCapContract)
                            totalPixels = (int)(-Options.JustifyCapContract * targetLength);
                    }
                    else
                    {
                        if (totalPixels / targetLength > Options.JustifyCapExpand)
                            totalPixels = (int)(Options.JustifyCapExpand * targetLength);
                    }


                    //work out how to spread pixles between character gaps and word spaces
                    if (charGaps == 0)
                    {
                        spacePixels = totalPixels;
                    }
                    else if (spaceGaps == 0)
                    {
                        charPixels = totalPixels;
                    }
                    else
                    {

                        if(contract)
                            charPixels = (int)(totalPixels * Options.JustifyCharacterWeightForContract * charGaps / spaceGaps);
                        else 
                            charPixels = (int)(totalPixels * Options.JustifyCharacterWeightForExpand * charGaps / spaceGaps);

         
                        if ((!contract && charPixels > totalPixels) ||
                            (contract && charPixels < totalPixels) )
                            charPixels = totalPixels;

                        spacePixels = totalPixels - charPixels;
                    }


                    int pixelsPerChar = 0;  //minimum number of pixels to add per char
                    int leftOverCharPixels = 0; //number of pixels remaining to only add for some chars

                    if (charGaps != 0)
                    {
                        pixelsPerChar = charPixels / charGaps;
                        leftOverCharPixels = charPixels - pixelsPerChar * charGaps;
                    }


                    int pixelsPerSpace = 0; //minimum number of pixels to add per space
                    int leftOverSpacePixels = 0; //number of pixels remaining to only add for some spaces

                    if (spaceGaps != 0)
                    {
                        pixelsPerSpace = spacePixels / spaceGaps;
                        leftOverSpacePixels = spacePixels - pixelsPerSpace * spaceGaps;
                    }

                    //now actually iterate over all nodes and set tweaked length
                    for (node = headNode; node != null; node = node.Next)
                    {

                        if (node.Type == TextNodeType.Space)
                        {
                            node.LengthTweak = pixelsPerSpace;
                            if (leftOverSpacePixels > 0)
                            {
                                node.LengthTweak += 1;
                                leftOverSpacePixels--;
                            }
                            else if (leftOverSpacePixels < 0)
                            {
                                node.LengthTweak -= 1;
                                leftOverSpacePixels++;
                            }


                        }
                        else if (node.Type == TextNodeType.Word)
                        {
                            int cGaps = (node.Text.Length - 1);
                            if (CrumbledWord(node))
                                cGaps++;

                            node.LengthTweak = cGaps * pixelsPerChar;


                            if (leftOverCharPixels >= cGaps)
                            {
                                node.LengthTweak += cGaps;
                                leftOverCharPixels -= cGaps;
                            }
                            else if (leftOverCharPixels <= -cGaps)
                            {
                                node.LengthTweak -= cGaps;
                                leftOverCharPixels += cGaps;
                            } 
                            else  
                            {
                                node.LengthTweak += leftOverCharPixels;
                                leftOverCharPixels = 0;
                            }
                        }

                        if ((!contract && node == expandEndNode) || (contract && node == contractEndNode))
                            break;

                    }

                }

            }


        }


        /// <summary>
        /// Checks whether to skip trailing space on line because the next word does not
        /// fit.
        /// 
        /// We only check one space - the assumption is that if there is more than one,
        /// it is a deliberate attempt to insert spaces.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="lengthSoFar"></param>
        /// <param name="boundWidth"></param>
        /// <returns></returns>
        private bool SkipTrailingSpace(TextNode node, float lengthSoFar, float boundWidth)
        {

            if (node.Type == TextNodeType.Space && node.Next != null && node.Next.Type == TextNodeType.Word && node.ModifiedLength + node.Next.ModifiedLength + lengthSoFar > boundWidth)
            {
                return true;
            }

            return false;

        }





        /// <summary>
        /// Prints text inside the given bounds.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="bounds"></param>
        /// <param name="alignment"></param>
        public void Print(string text, float maxWidth, QFontAlignment alignment)
        {
            var processedText = ProcessText(text, maxWidth, alignment);
            Print(processedText);
        }





        /// <summary>
        /// Creates node list object associated with the text.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public ProcessedText ProcessText(string text, float maxWidth, QFontAlignment alignment)
        {
            //TODO: bring justify and alignment calculations in here
            
            var nodeList = new TextNodeList(text);
            nodeList.MeasureNodes(fontData, Options);

            //we "crumble" words that are two long so that that can be split up
            var nodesToCrumble = new List<TextNode>();
            foreach (TextNode node in nodeList)
                if (node.Length >= maxWidth && node.Type == TextNodeType.Word)
                    nodesToCrumble.Add(node);

            foreach (var node in nodesToCrumble)
                nodeList.Crumble(node, 1);

            //need to measure crumbled words
            nodeList.MeasureNodes(fontData, Options);


            var processedText = new ProcessedText();
            processedText.textNodeList = nodeList;
            processedText.maxWidth = maxWidth;
            processedText.alignment = alignment;


            return processedText;
        }




        /// <summary>
        /// Prints text as previously processed with a boundary and alignment.
        /// </summary>
        /// <param name="processedText"></param>
        public void Print(ProcessedText processedText)
        {
            renderer.Begin(PointF.Empty);
            PrintOrMeasure(processedText, false);
            renderer.End();
        }



        private SizeF PrintOrMeasure(ProcessedText processedText, bool measureOnly)
        {
            float maxMeasuredWidth = 0f;
            
            float maxWidth = processedText.maxWidth;
            var alignment = processedText.alignment;


            //TODO - use these instead of translate when rendering by position (at some point)
            float xPos = 0f;
            float yPos = 0f;


            float xOffset = xPos;
            float yOffset = yPos;

            var nodeList = processedText.textNodeList;
            for (TextNode node = nodeList.Head; node != null; node = node.Next)
                node.LengthTweak = 0f;  //reset tweaks


            if (alignment == QFontAlignment.Right)
                xOffset -= (float)Math.Ceiling(TextNodeLineLength(nodeList.Head, maxWidth) - maxWidth);
            else if (alignment == QFontAlignment.Centre)
                xOffset -= (float)Math.Ceiling(0.5f * TextNodeLineLength(nodeList.Head, maxWidth) );
            else if (alignment == QFontAlignment.Justify)
                JustifyLine(nodeList.Head, maxWidth);




            bool atLeastOneNodeCosumedOnLine = false;
            float length = 0f;
            for (TextNode node = nodeList.Head; node != null; node = node.Next)
            {
                bool newLine = false;

                if (node.Type == TextNodeType.LineBreak)
                {
                    newLine = true;
                }
                else
                {

                    if (SkipTrailingSpace(node, length, maxWidth) && atLeastOneNodeCosumedOnLine)
                    {
                        newLine = true;
                    }
                    else if (length + node.ModifiedLength <= maxWidth || !atLeastOneNodeCosumedOnLine)
                    {
                        atLeastOneNodeCosumedOnLine = true;
                        if(!measureOnly)
                            RenderWord(xOffset + length, yOffset, node);
                        length += node.ModifiedLength;

                        maxMeasuredWidth = Math.Max(length, maxMeasuredWidth);

                    }
                    else
                    {
                        newLine = true;
                        if (node.Previous != null)
                            node = node.Previous;
                    }

                }

                if (newLine)
                {

                    yOffset += LineSpacing;
                    xOffset = xPos;
                    length = 0f;
                    atLeastOneNodeCosumedOnLine = false;

                    if (node.Next != null)
                    {
                        if (alignment == QFontAlignment.Right)
                            xOffset -= (float)Math.Ceiling(TextNodeLineLength(node.Next, maxWidth) - maxWidth);
                        else if (alignment == QFontAlignment.Centre)
                            xOffset -= (float)Math.Ceiling(0.5f * TextNodeLineLength(node.Next, maxWidth) );
                        else if (alignment == QFontAlignment.Justify)
                            JustifyLine(node.Next, maxWidth);
                    }
                }

            }

            return new SizeF(maxMeasuredWidth, yOffset + LineSpacing - yPos);
        }

        /// <summary>
        /// Dispose of the QFont data.
        /// </summary>
        public virtual void Dispose()
        {
            fontData.Dispose();
        }

    }
}
