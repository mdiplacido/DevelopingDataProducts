map <- read.csv("c:\\temp\\countrycache\\Map", sep="\t")
map$SequenceGroup <- as.factor(map$SequenceGroup)

scores <- read.csv("c:\\temp\\countrycache\\Scores")
scoresMean <- mean(scores$Score)
scoresStdDev <- sd(scores$Score)

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

input <- "C,G,D,E,F"
input <- unlist(strsplit(input, ","))

score <- computeScore(generateNgramSequences(input))
numStdDev <- abs((score - scoresMean) / scoresStdDev)

print(paste("Your score was:", round(score, 2)))
print(paste(paste("Your score was", round(numStdDev, 2)), "standard deviations from the mean"))

hist(scores$Score, xlim=c(0,min(score + 5, 100)), xlab="Score", main="Your score vs. the model")
lines(c(score, score), c(0, 30), col="red", lwd=5)
