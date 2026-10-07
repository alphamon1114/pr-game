"""Instantiate the included OFL variable font for Unity's runtime text engine.

Requires fonttools. The source font's default axis is weight 100; UI uses 400.
Run from the repository root: python ArtSource/Desktop/create_regular_font.py
"""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

root = Path(__file__).resolve().parents[2]
fonts = root / "Assets/Resources/Fonts"
font = TTFont(fonts / "NotoSansKR.ttf")
instantiateVariableFont(font, {"wght": 400}, inplace=True)
font.save(fonts / "NotoSansKR-Regular.ttf")
