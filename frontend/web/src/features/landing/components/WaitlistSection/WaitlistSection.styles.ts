import styled from '@emotion/styled'
import { MutedText, SectionTitle } from '../../../../styles/components/texts'

export const WaitlistTitle = styled(SectionTitle)`
  margin: 0 0 4px;
  font-size: clamp(20px, 2.6vw, 26px);
  font-weight: 600;
  text-align: left;

  @media (max-width: 900px) {
    text-align: center;
  }
`

export const WaitlistBody = styled(MutedText)`
  margin: 0;
  font-size: 15px;
  line-height: 1.6;

  @media (max-width: 600px) {
    font-size: 14px;
  }
`
