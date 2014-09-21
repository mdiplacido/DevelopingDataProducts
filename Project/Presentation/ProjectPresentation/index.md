---
title       : Is My Song Country?
subtitle    : A tool that can judge the chords in your song for "countryness"
author      : mdiplacido
job         : 
framework   : io2012        # {io2012, html5slides, shower, dzslides, ...}
highlighter : highlight.js  # {highlight.js, prettify, highlight}
hitheme     : tomorrow      # 
widgets     : []            # {mathjax, quiz, bootstrap}
mode        : selfcontained # {standalone, draft}
knit        : slidify::knit2slides
---

## Motivation

* I play guitar, and I wondered if I could write an app to judge my song chord sequences.
* Could I write a simple ShinyApp that could determine if my song uses country chord sequences?
* If you like country this app will give you a sense for how "Country" your chord sequences are
* If you don't like country then you can use this app to avoid country chord sequences

---

## How it works

* Scrape 100+ popular country songs from http://www.countrytabs.com/
* Learn a model for the sequences of chords per song
* For example a song could have the following chord sequences:
  * D -> DaddC -> Em7 -> Gm+Bb -> C -> F -> Esus4 -> E
* The model would learn the following sequences:
  * D
  * D -> DaddC
  * D -> DaddC -> Em7
  * DaddC -> Em7 -> Gm+Bb
  * Em7 -> Gm+Bb -> C
  * Gm+Bb -> C -> F
  * C -> F -> Esus4
  * F -> Esus4 -> E