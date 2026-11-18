import styled from '@emotion/styled'
import { MutedText, PageTitle } from '../../../../styles/components/texts.styles'

export const HeroTitle = styled(PageTitle)`
  margin: 0;
  max-width: 620px;
  font-family: var(--font-family);
  font-size: var(--fs-display-lg);
  font-weight: 600;
  line-height: 1.06;
  letter-spacing: -0.025em;

  span {
    color: var(--color-accent);
  }

  @media (max-width: 900px) {
    max-width: 700px;
  }

  @media (max-width: 600px) {
    font-size: var(--fs-30);
  }
`

export const HeroBody = styled(MutedText)`
  max-width: 520px;
  margin: 0;
  font-size: var(--fs-17);
  line-height: 1.65;

  @media (max-width: 900px) {
    max-width: 700px;
  }

  @media (max-width: 600px) {
    font-size: var(--fs-16);
  }
`
