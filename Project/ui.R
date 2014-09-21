# install.packages("shiny")
library(shiny)

chords <- read.csv("Chords", header=F)
chords <- chords[order(chords$V1),]
chordsStr <- as.character(chords)

generateItemList <- function()
{
  listItems <- ""
  for (i in 1:length(chordsStr))
  {
    listItems <- paste(listItems, "<LI>", chordsStr[i], "</LI>")
  }
  return(HTML(paste("<UL>", listItems, "</UL>")))
}

shinyUI(pageWithSidebar(
  headerPanel("Is my song country-ish?"),
  sidebarPanel(
      p("Enter some chords in this textbox, the server will rate your song."),
      p("A score closer to 0 means your song is very country, a score near 100 means your song is nothing like country."),
      textInput(value="C, G, D", inputId="chords", label="Enter your song chords:"),
      br(),
      strong("Note:"),
      span("you might need to click the 'Rate my song!' button more than 1 time.  Sorry about that."),
      p(),
      submitButton(text="Rate my song!"),
      h3("Chord Dictionary"),
      p("Here's a list of chords that the model trained on, you can use this list as a reference to build your song, but feel free to enter any chord, we'll still score your song"),
      div(generateItemList(), style="height:300px; overflow-y: scroll;")
    ),
  mainPanel(
      htmlOutput("scoreSummary"),
      plotOutput('songHist'),
      h4("you entered the following chords: "),
      htmlOutput("parsedChords")
    )
  ))

