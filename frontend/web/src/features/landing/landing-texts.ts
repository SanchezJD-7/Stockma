import styled from '@emotion/styled'
import { MutedText, SectionTitle } from '../../styles/components/texts'

export const LandingSectionTitle = styled(SectionTitle)`
  max-width: 720px;
  margin: 0 auto 16px;
  font-size: clamp(28px, 4vw, 44px);
  line-height: 1.1;
  text-align: center;
`

export const LandingLead = styled(MutedText)`
  max-width: 720px;
  margin: 0 auto 56px;
  text-align: center;
  font-size: 17px;
`
