Hi,
This repository contains the most relevant scripts to the final version of my project. The scripts include, but are not limited to:

Champ_anneau : A script that calculates the off-axis magnetic field at any point in space for a loop of current. The script computes the vector potential A as well as the magnetic field B directly,
using special functions that have to do with elliptic integrals. It also generates 2D heatmaps of those physical values, generally meant for prototyping.

CurrentModelsV2 : C# script of the second version of a series of functions that compute current as a function of time according to different mathematical methods. Three models are coded : rectangular, sinusoidal and a gaussian curve. The rectangular current approach creates violent, unrealistic acceleration of the payload because of the immediate increase in amperage, but it proved useful in early development to kick things off. The gaussian approach is perhaps the more "standard" model for this kind of situation, and the sinusoid was briefly tinkered with. 

