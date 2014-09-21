library(shiny)
library(ggplot2)

map <- read.csv("Map", sep="\t")
map$SequenceGroup <- as.factor(map$SequenceGroup)

scores <- read.csv("Scores")
scoresMean <- mean(scores$Score)
scoresStdDev <- sd(scores$Score)
scoresMax <- max(scores$Score)

chords <- read.csv("Chords", header=F)
chords <- chords[order(chords$V1),]
chordsStr <- as.character(chords)

generateNgramSequences <- function(sequence)
{
  ngramLength <- 3
  sequences <- list()
  
  for(i in 1:length(sequence))
  {
    current <- ""
    if (i > ngramLength)
    {
      current <- paste(sequence[(i-(ngramLength - 1)):i], collapse=",")
    }
    else
    {
      current <- paste(sequence[1:i], collapse=",")
    }
    
    sequences[i] <- current
  }
  
  invisible(sequences)
}

computeScore <- function(sequences)
{
  score <- 0.0
  for(i in 1:length(sequences))
  {
    lookup <- unlist(sequences[i])
    print(lookup)
    match <- map[which(map$Sequence == lookup), ]
    if (nrow(match) == 0)
    {
      score <- score + 100
    }
    else
    {
      score <- score + match$ProbabilityNullHypothesisNegativeLog
    }
    
    print(score)
  }
  
  score <- score / length(unlist(sequences))
  invisible(score)
}

trim <- function (x) gsub("^\\s+|\\s+$", "", x)

shinyServer(
    function(input, output)
    {
        theChords <- reactive({
          theParsedChords <- unlist(strsplit(input$chords, ",")) 
          for(i in 1:length(theParsedChords))
          {
            theParsedChords[i] <- trim(theParsedChords[i])
          }
          return(theParsedChords)
        })
        
        output$parsedChords <- renderUI({
          chordsTmp <- theChords()
          listItems <- ""
          for (i in 1:length(chordsTmp))
          {
            listItems <- paste(listItems, "<LI>", chordsTmp[i], "</LI>")
          }
          return(HTML(paste("<UL>", listItems, "</UL>")))
        })
        
        songScore <- reactive({
          tmpChords <- theChords()
          score <- computeScore(generateNgramSequences(tmpChords))
          return(score)
        })
        
        output$songScore <- renderText({songScore()})
        
        scoreNumStdDev <- reactive({
          scoreTmp <- songScore()
          numStdDev <- abs((scoreTmp - scoresMean) / scoresStdDev)
          return(numStdDev)
        })
        
        output$scoreSummary <- renderUI({
          scoreTmp <- songScore()
          firstLine <- paste("<p>Your score was:<strong>", round(scoreTmp, 2), "</strong></p>")
          secondLine <- paste("<p>Your score was<strong>", round(scoreNumStdDev(), 2), "</strong>standard deviations from the mean song score")
          return(HTML(paste(firstLine, secondLine)))
        })
        
        output$songHist <- renderPlot({
          scoreTmp <- songScore()
          scoresMeanTmp <- scoresMean
          
          environmentTmp<-environment() 

          cut1 <- data.frame(Songs="Average country song", vals=c(scoresMeanTmp))
          cut2 <- data.frame(Songs="Your song", vals=c(scoreTmp))
          cuts <- rbind(cut1,cut2)
          
          thePlot <- ggplot(scores, aes(x=Score), environment=environmentTmp) + 
            geom_histogram(binwidth=.5, alpha=.7, colour="white") + 
            geom_vline(data=cuts, aes(xintercept=vals, 
                                      linetype=Songs,
                                      colour = Songs),
                       show_guide = TRUE, size=1.5) +

            xlab("Score") + ylab("number of training songs") + 
            ggtitle("Training song score distribution\n")
          
          return(thePlot)
        })
    }
  )