import styled from '@emotion/styled'
import { MutedText, SectionTitle } from '../../../../styles/components/texts.styles'

export const WaitlistTitle = styled(SectionTitle)`
  margin: 0 0 4px;
  font-size: var(--fs-bar-title);
  font-weight: 600;
  text-align: left;

  @media (max-width: 900px) {
    text-align: center;
  }
`

export const WaitlistBody = styled(MutedText)`
  margin: 0;
  font-size: var(--fs-15);
  line-height: 1.6;

  @media (max-width: 600px) {
    font-size: var(--fs-14);
  }
`
