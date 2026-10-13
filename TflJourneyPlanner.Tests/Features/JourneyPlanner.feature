Feature: Plan a journey widget
  As a TfL customer
  I want to plan a journey between two locations
  So that I can choose how to travel and know what to expect on the way

  Background:
    Given I am on the TfL Plan a journey page

  # Scenario 1
  @smoke
  Scenario: A valid journey shows the walking and cycling times
    When I type "Leicester Sq" in the From field and select "Leicester Square Underground Station" from the suggestions
    And I type "Covent Gar" in the To field and select "Covent Garden Underground Station" from the suggestions
    And I plan my journey
    Then journey results are shown from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    And the walking time is 6 minutes
    And the cycling time is 1 minute
    And the cycling distance is "0.4km"

  # Scenario 1 (continued): walking time for each walking speed.
  # Walking and cycling times come from distance and speed, not live train data,
  # so exact values are stable and can be asserted directly.
  @smoke
  Scenario Outline: The walking time matches the walking speed selected
    Given I have planned a journey with:
      | Field | Search text  | Suggestion                           |
      | From  | Leicester Sq | Leicester Square Underground Station |
      | To    | Covent Gar   | Covent Garden Underground Station    |
    When I change my walking speed to "<speed>"
    Then the results are for a "<speed>" walking speed
    And the walking journey takes <minutes> minutes

    Examples:
      | speed   | minutes |
      | Slow    | 9       |
      | Average | 6       |
      | Fast    | 5       |

  # Scenario 2
  @preferences
  Scenario: Updating preferences to routes with least walking shows valid journey times
    Given I have planned a journey with:
      | Field | Search text  | Suggestion                           |
      | From  | Leicester Sq | Leicester Square Underground Station |
      | To    | Covent Gar   | Covent Garden Underground Station    |
    When I edit my preferences to show routes with least walking
    And I update the journey
    Then the results are showing "Showing routes with least walking"
    And every journey option has a total time that matches its departure and arrival times
    And every journey option takes less than 20 minutes

  # Scenario 3
  @accessibility
  Scenario: Journey details show the access information at Covent Garden
    Given I have planned a journey with:
      | Field | Search text  | Suggestion                           |
      | From  | Leicester Sq | Leicester Square Underground Station |
      | To    | Covent Gar   | Covent Garden Underground Station    |
    And I have updated my preferences to show routes with least walking
    When I view the details of journey option 1
    Then the access information at "Covent Garden Underground Station" is:
      | Access information |
      | Up stairs          |
      | Up lift            |
      | Level walkway      |

  # Scenario 4
  @negative
  Scenario Outline: No results are shown when both locations are invalid
    When I enter "<from>" in the From field without selecting a suggestion
    And I enter "<to>" in the To field without selecting a suggestion
    And I plan my journey
    Then no journey results are shown and the message "<message>" is displayed

    # Note: the site currently shows two different messages for very similar invalid input.
    # See "Observations" in the README.
    Examples:
      | from     | to       | message                                                                     |
      | @@@###   | &&&$$$   | Journey planner could not find any results to your search. Please try again |
      | ~~~~~~~~ | ^^^^^^^^ | Sorry, we can't find a journey matching your criteria                       |

  @negative
  Scenario: No results are shown when one of the locations is invalid
    When I enter "@@@###" in the From field without selecting a suggestion
    And I type "Covent Gar" in the To field and select "Covent Garden Underground Station" from the suggestions
    And I plan my journey
    Then no journey results are shown and the message "Journey planner could not find any results to your search. Please try again" is displayed

  # Scenario 5
  @negative
  Scenario: A journey cannot be planned when no locations are entered
    Given I have not entered a From or To location
    When I plan my journey
    Then the From field shows the error "The From field is required."
    And the To field shows the error "The To field is required."
    And I remain on the Plan a journey page
